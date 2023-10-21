grammar MountDateTime;

datetime  : date ',' time '#' ;

// HH:MM.M# 
// HH:MM:SS#
// HH:MM:SS.S#
// HH:MM:SS.SS#

time  :  hours ':' minutes ':' tenth_minutes
	  | hours ':' minutes ':' seconds
	  | hours ':' minutes ':' seconds '.' tenth_seconds
      | hours ':' minutes ':' seconds '.' hundredth_seconds
      ;

// MM/DD/YY#
// MM:DD:YY#
// YYYY-MM-DD#
date  :	 months '/' days ':' years
	  |  months ':' days ':' years
	  |  years '-' months '-' days
	  ;

hours : INTEGER ;
minutes : INTEGER ;
seconds : INTEGER ;
tenth_minutes : INTEGER ;
hundredth_seconds : INTEGER ;
tenth_seconds : INTEGER ;

years : INTEGER ;
months : INTEGER ;
days : INTEGER ;

fragment DIGIT : [0-9] ;
INTEGER : DIGIT+ ;