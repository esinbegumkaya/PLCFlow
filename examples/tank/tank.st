PROGRAM TankController
VAR
    LowLevel : BOOL;
    HighLevel : BOOL;
    Pump : BOOL;
END_VAR

IF LowLevel THEN
    Pump := TRUE;
END_IF;

IF HighLevel THEN
    Pump := FALSE;
END_IF;
END_PROGRAM
