PROGRAM TrafficLight
VAR
    Enable : BOOL;
    Red : BOOL;
    Yellow : BOOL;
    Green : BOOL;
END_VAR

IF Enable THEN
    Red := FALSE;
    Green := TRUE;
ELSE
    Red := TRUE;
    Green := FALSE;
END_IF;
END_PROGRAM
