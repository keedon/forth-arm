
default: forth

forth.o: forth.s
	as -g -o forth.o forth.s -aml=f.out

forth: forth.o
	gcc -g -o forth forth.o -lc

clean:
	rm -f *.o forth

