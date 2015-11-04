.code
mov	R2, 0x3FC4    ;switches
mov	R3, 0x3FD0    ;rleds
mov	R4, 0x3FD4    ;gleds
loop:
ldw	R8, R2, 0
stw	R8, R3, 0
not	R8
stw	R8, R4, 0
bra	loop
