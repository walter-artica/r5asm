.code
set		R1, 0
cmp		R1, 0
bz		label1
set		R1, 1
bt		label2
label1:
set		R1, 2
label2:
bt		label2
nop
nop
nop
