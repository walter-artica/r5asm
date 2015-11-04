.code
set     R2, 0xFFFFFFC4
loop:
ldw     R3, R2, 0
stw     R3, R2, 0
bt      loop
set     R4, 0xFFFF
stw     R4, R2, 0
