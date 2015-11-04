.code
set	R2, 0xFFFFFFC8
set	R3, 0xFFFFFFC0

nop
clr     R8
cycle:
ldw	R4, R3, 0
inc	R4, 500
wait:
ldw	R5, R3, 0
cmp	R5, R4
bb	wait
inc     R8
stw     R8, R2, 0
bt      cycle
