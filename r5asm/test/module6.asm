.code
	mov	R8, 0x3FC8    ;UART data
	mov	R9, 0x3FCC    ;UART status

waitforrx:
	ldw	R0, R9, 0
	and	R0, R0, 1
	cmp	R0, R0, 0
	beq	waitforrx
	ldw	R0, R8, 0
waitfortx:
	ldw	R1, R9, 0
	and	R1, R1, 2
	cmp	R1, R1, 0
	beq	waitfortx
	stw	R0, R8, 0
	
	bra	waitforrx
