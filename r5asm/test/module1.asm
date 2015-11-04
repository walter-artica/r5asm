.define	const1	255
.define longconst 1048578
.define CODELEN 256

;.export main
;.import extsub

.data
s1:     .byte   "Bye, world.", 0
var1:	.word   -1, 0, 1
b1:     .byte   1
b2:     .byte   2
h1:     .hword  0x1000
h2:     .hword  0x1000
b3:     .byte   2
var2:	.word   0xFFFFFFFF
var3:   .word   4294967295
same_b4:
b4:     .byte   0, 1, 2, 3, 4, 5
rb1:    .resb   2
end_data:

.udata
uvar1:	.word
ub1:    .byte
uw1:    .hword
same1:
same2:
uw2:    .hword
uvar2:  .word
uw3:    .hword
urb1:   .resb   2
end_udata:

.code
main:
	call	R8
	call	R8, R9
	call	R8, 256
	call	R8, 32768
	call	R8, 32767
	call    sub1
	jmp     R9
	nop
	bt	label1
label1:
	bt	label1
	bt      label1
	add	R8, R2, 32768
	add	R8, R2, 32767
	add	R8, R2, 6
	add	R8, R2, R3
	cmp	R5, R6
	cmp	R6, 32768
	cmp	R6, 32767
	cmp	R6, -1
	cmp	R6, 10
	sethi	R15, HI(longconst+1)
	set	R15, LO(longconst+1)
	set	R16, CODELEN
	seta	R17, uvar1
	add	R1, R2, R3
	set	R0, R2
	set	R1, R3
	set	R1, 65535
	set	R1, 65536
	set	R1, -1
	set	R1, 1
	bt	label2
loop:
	ldw	R2, R0, 0
	stw	R2, R15, 0
	set	R2, 6
label2:
	ldw	R3, R15, 0
	mul	R3, R3, 2
	stw	R3, R1, 0
	bt	exit
	seta	R1, s1
	bt	loop
exit:

sub1:
	set     R0, R0
	ret