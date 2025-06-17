## actual signals:
On start I have receivied this:
Device 1 Line: 0003 COM 10:36:55:08 01

and on finish I can see this:
Device 1 Line: 0003 c1M 00005.22 01


12 Interfaces
12.1 RS232 Interface
Output format: 1 start bit, 8 data bit, no parity bit, 1 stop bit
Bit rate: 9 600 baud factory setting
adjustable: 2400, 4800, 9600, 19200, 28800, 38400
Transmisson protocol: ASCII
yNNNNxCCCxHH:MM:SS.zhtqxGGRRRR(CR)
y first sign is blank or info (see below)
x blank
NNNN start number, max. 4-digit, prezeros arel not shown
CCC channels of timing device
c0 channel 0 start channelS
c0M channel 0 triggered by keypad <START>
c1 channel 1 finish channel
c1M channel 1 triggered by keypad <STOP>
c2 channel 2
c3 channel 3
c4 channel 4
c5 channel 5
c6 channel 6
c7 channel 7
c8 channel 8
RT run time
TT total time
SQ sequential time (lap time)
kmh speed measurement (possible displays: km/h, m/s, mph)
HH:MM:SS.zhtq time in hours, minutes, seconds and 1/10 000 seconds
GG group, lap or blank
RRRR rank (only at classement available)
(CR) carriage return
Info – the following figures may be in first position:
x blank
? time without valid start number
m time from memory
c times deleted (e.g. with CLEAR button)
C memory time deleted (e.g. with CLEAR button)
d times deleted due to disqualification
i manually entered time with <INPUT>
n enter new start number
Example of a RS 232 interface output (e.g. program backup)
0001 c0 15:43:49,8863 00
 0002 c0 15:43:50,1647 00
 0005 c1 15:43:51,6464 00
 0006 c0 15:43:51,9669 00
 0007 c1 15:43:52,2467 00
 0008 c0 15:43:52,4579 00
 0009 c1 15:43:52,6941 00
 0015 c0M 15:43:55,6200 00
 0016 c1M 15:43:55,8800 00
 0019 c0M 15:43:57,020 00
m0007 c0 15:43:59,9927 00
m 0008 c1 15:44:00,2849 00
m 0009 c0 15:44:00,5499 00
m 0010 c1 15:44:00,8182 00
m 0011 c0 15:44:01,0366 00
C 0011 c0 15:44:01,0366 00
n 0014 c0 15:44:01,0366 00
 0020 c0 15:44:15,0077 00
 0022 c0 15:44:15,5165 00
 0023 c1 15:44:15,7847 00
c 0023 c1 15:44:15,7847 00
i 0023 c1 15:44:15,7847 00
