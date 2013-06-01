\ Forth ARM
\ (c) 2012 Braden Shepherdson
\ Version 3

\ This is a Forth system
\ designed to run on ARMv6
\ systems. It consists of this
\ source code file and a binary
\ executable.

: /MOD 2DUP MOD -ROT / ;

: NL 10 ;
: BL 32 ;
: CR NL EMIT ;
: SPACE BL EMIT ;

: NEGATE 0 SWAP - ;

\ Standard words for booleans
: TRUE 1 ;
: FALSE 0 ;
: NOT 0= ;


\ LITERAL compiles LIT <foo>
: LITERAL IMMEDIATE
    ' LIT , \ compile LIT
    ,       \ compile literal
  ;

\ Idiom: [ ] and LITERAL to
\ compute at compile time.
\ Me: This seems dubious. The
\ dict is getting longer.
: ';' [ CHAR ; ] LITERAL ;
: '(' [ CHAR ( ] LITERAL ;
: ')' [ CHAR ) ] LITERAL ;


\ Compiles IMMEDIATE words.
: [COMPILE] IMMEDIATE
    WORD  \ get the next word
    FIND  \ find it in the dict
    >CFA  \ get its codeword
    ,     \ and compile it.
  ;


: RECURSE IMMEDIATE
    LATEST @  \ This word
    >CFA      \ get codeword
    ,         \ compile it
  ;

\ Control structures - ONLY SAFE IN COMPILED CODE!
\ cond IF t THEN rest
\ -> cond 0BRANCH OFFSET t rest
\ cond IF t ELSE f THEN rest
\ -> cond 0BRANCH OFFSET t
\      BRANCH OFFSET2 f rest
: IF IMMEDIATE
    ' 0BRANCH ,
    HERE @      \ save location
    0 ,         \ dummy offset
  ;


: THEN IMMEDIATE
    DUP
    HERE @ SWAP - \ calc offset
    SWAP !        \ store it
  ;

: ELSE IMMEDIATE
    ' BRANCH , \ branch to end
    HERE @     \ save location
    0 ,        \ dummy offset
    SWAP       \ orig IF offset
    DUP        \ like THEN
    HERE @ SWAP -
    SWAP !
  ;


\ BEGIN loop condition UNTIL ->
\ loop cond 0BRANCH OFFSET
: BEGIN IMMEDIATE
    HERE @
;

: UNTIL IMMEDIATE
    ' 0BRANCH ,
    HERE @ -
    ,
;


\ BEGIN loop AGAIN, infinitely.
: AGAIN IMMEDIATE
    ' BRANCH ,
    HERE @ -
    ,
  ;

\ UNLESS is IF reversed
: UNLESS IMMEDIATE
    ' NOT ,
    [COMPILE] IF
  ;



\ BEGIN cond WHILE loop REPEAT
: WHILE IMMEDIATE
    ' 0BRANCH ,
    HERE @
    0 , \ dummy offset
  ;

: REPEAT IMMEDIATE
    ' BRANCH ,
    SWAP
    HERE @ - ,
    DUP
    HERE @ SWAP -
    SWAP !
  ;


: ( IMMEDIATE
    1 \ tracking depth
    BEGIN
        KEY \ read next char
        DUP 40 = IF \ open (
            DROP \ drop it
            1+ \ bump the depth
        ELSE
            41 = IF \ close )
               1- \ dec depth
            THEN
        THEN
    DUP 0= UNTIL \ depth == 0
    DROP \ drop the depth
  ;

: NIP ( x y -- y ) SWAP DROP ;
: TUCK ( x y -- y x y )
    SWAP OVER ;
: PICK ( x_u ... x_1 x_0 u --
    x_u ... x_1 x_0 x_u )
    1+     \ skip over u
    DSP@ + \ add to DSP
    @      \ fetch
  ;


\ writes n spaces to stdout
: SPACES ( n -- )
    BEGIN
        DUP 0> \ while n > 0
    WHILE
        SPACE 1-
    REPEAT
    DROP
;

\ Standard base changers.
: DECIMAL ( -- ) 10 BASE ! ;
: HEX ( -- ) 16 BASE ! ;


\ Strings and numbers
: U.  ( u -- )
    BASE @ /MOD \ ( width r q )
    ?DUP IF \ if q <> 0 then
        RECURSE \ print quot
    THEN
    \ print the remainder
    DUP 10 < IF
        48 \ dec digits 0..9
    ELSE
        10 -
        65 \ hex and other A..Z
    THEN
    + EMIT
;

\ Debugging utility.
: .S ( -- )
    DSP@ \ get stack pointer
    BEGIN
        DUP S0 @ <
    WHILE
        DUP @ U. \ print
        SPACE
        1+       \ move up
    REPEAT
    DROP
;


: UWIDTH ( u -- width )
    BASE @ / \ rem quot
    ?DUP IF   \ if quot <> 0
        RECURSE 1+
    ELSE
        1 \ return 1
    THEN
;



: U.R ( u width -- )
    SWAP   \ ( width u )
    DUP    \ ( width u u )
    UWIDTH \ ( width u uwidth )
    ROT    \ ( u uwidth width )
    SWAP - \ ( u width-uwdith )
    SPACES \ no-op on negative
    U.
;


\ Print padded, signed number
: .R ( n width -- )
    SWAP DUP 0< IF
        NEGATE \ width u
        1 SWAP \ width 1 u
        ROT 1- \ 1 u width-1
    ELSE
        0 SWAP ROT \ 0 u width
    THEN
    SWAP DUP \ flag width u u
    UWIDTH \ flag width u uw
    ROT SWAP - \ ? u w-uw
    SPACES SWAP \ u ?
    IF 45 EMIT THEN \ print -
    U. ;

: . 0 .R SPACE ;
\ Replace U.
: U. U. SPACE ;
\ ? fetches an addr and prints
: ? ( addr -- ) @ . ;



\ c a b WITHIN ->
\   a <= c & c < b
: WITHIN ( c a b -- ? )
    >R \ c a
    2DUP < IF
        2DROP FALSE EXIT
    THEN
    DROP R> \ c b
    < ;

: DEPTH ( -- n )
    S0 @ DSP@ -
    1- \ adjust for S0 on stack
;


: C,
    HERE @ !
    1 HERE +! \ Increment HERE
;


: .S_COMP
    ' LITSTRING ,
    HERE @ \ address
    0 ,    \ dummy length
    BEGIN
        KEY        \ next char
        DUP 34 <>  \ ASCII "
    WHILE
        C, \ copy character
    REPEAT
    DROP \ drop the "
    DUP HERE @ SWAP - \ length
    1- SWAP ! \ set length
  ;

: .S_INTERP
    HERE @ \ temp space
    BEGIN
        KEY
        DUP 34 <>  \ ASCII "
    WHILE
        OVER ! \ save character
        1+     \ bump address
    REPEAT
    DROP     \ drop the "
    HERE @ - \ calculate length
    HERE @   \ push start addr
    SWAP     \ addr len
  ;

: S" IMMEDIATE ( -- addr len )
    STATE @ IF \ compiling?
        .S_COMP
    ELSE \ immediate mode
        .S_INTERP
    THEN
  ;



: ." IMMEDIATE ( -- )
    STATE @ IF \ compiling?
        [COMPILE] S"
        ' TELL ,
    ELSE
        \ Just read and print
        BEGIN
            KEY
            DUP 34 = IF \ "
                DROP EXIT
            THEN
            EMIT
        AGAIN
    THEN
  ;


: CONSTANT
    WORD CREATE
    DOCOL , \ codeword
    ' LIT , \ append LIT
    ,       \ input value
    ' EXIT , \ and append EXIT
  ;
: VALUE ( n -- )
    WORD CREATE
    DOCOL ,
    ' LIT ,
    ,
    ' EXIT ,
  ;

\ Allocates n bytes of memory
: ALLOT ( n -- addr )
    HERE @ SWAP \ here n
    HERE +!     \ add n to HERE
  ;

\ Converts a number of cells into a number of bytes
: CELLS ( n -- n ) 4 * ;

\ Finally VARIABLE itself.
: VARIABLE
    1 CELLS ALLOT \ allocate 1 cell
    WORD CREATE
    DOCOL ,
    ' LIT ,
    , \ pointer from ALLOT
    ' EXIT ,
  ;

\ FORGET is a horrible hack to
\ deallocate memory.
\ Sets HERE to the beginning of
\ the given word and resets
\ LATEST. FORGETing built-ins
\ will cause suffering.
: FORGET
    WORD FIND \ dict address
    DUP @ LATEST !
    HERE !
  ;


: CASE IMMEDIATE 0 ;
: OF IMMEDIATE
    ' OVER ,
    ' = ,
    [COMPILE] IF
    ' DROP ,
;
: ENDOF IMMEDIATE
    [COMPILE] ELSE ;
: ENDCASE IMMEDIATE
    ' DROP ,
    BEGIN ?DUP WHILE
    [COMPILE] THEN REPEAT
;

: :NONAME
    0 0 CREATE \ nameless entry
    HERE @     \ current HERE
    \ value is the address of
    \ the codeword, ie. the xt
    DOCOL ,
    ] \ compile the definition.
  ;

\ compiles in a LIT
: ['] IMMEDIATE ' LIT , ;


\ Expects the user to specify the number of bytes, not cells.
: ARRAY ( n -- )
  ALLOT >R
  WORD CREATE \ define the word
  DOCOL ,    \ compile DOCOL
  ' LIT ,    \ compile LIT
  R> ,       \ compile address
  ' + ,      \ add index
  ' EXIT ,   \ compile EXIT
;


: WELCOME
    ." FORTH ARM" CR
    ." by Braden Shepherdson" CR
    ." version " VERSION . CR
;

WELCOME
\ HIDE WELCOME



: DO IMMEDIATE \ lim start --
  HERE @
  ' 2DUP ,
  ' SWAP , ' >R , ' >R ,
  ' > ,
  ' 0BRANCH ,
  HERE @ \ location of offset
  0 , \ dummy exit offset
;


: +LOOP IMMEDIATE \ inc --
  ' R> , ' R> , \ i s l
  ' SWAP , \ ils
  ' ROT , ' + , \ l s'
  ' BRANCH , \ ( top branch )
  SWAP HERE @ \ ( br top here )
  - , \ top ( br )
  HERE @ OVER -
  SWAP ! \ end
  ' R> , ' R> , ' 2DROP ,
;

: LOOP IMMEDIATE \ --
  ' LIT , 1 , [COMPILE] +LOOP ;

: I \  -- i
  R> R> \ ret i
  DUP -ROT >R >R ;

: J \ -- j
  R> R> R> R> DUP \ ( riljj )
  -ROT \ ( r i j l j )
  >R >R \ ( r i j )
  -ROT >R >R \ ( j )
;

\ Drops the values from RS.
: UNLOOP \ ( -- )
  R> R> R> 2DROP >R ;
