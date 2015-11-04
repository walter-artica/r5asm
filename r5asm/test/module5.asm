.code
start:
mov	R1, -4
mov	R2, -8
mov	R3, -12
loop:
ldw	R0, R1, 0
stw	R0, R1, 0
stw	R0, R3, 0
bra	start
