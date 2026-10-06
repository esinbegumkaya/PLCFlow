PROGRAM ConveyorControl
VAR
    StartButton : BOOL;
    StopButton : BOOL;
    Motor : BOOL;
    Counter : INT;
END_VAR

IF StartButton THEN
    Motor := TRUE;
END_IF;

IF StopButton THEN
    Motor := FALSE;
END_IF;

IF Motor THEN
    Counter := Counter + 1;
END_IF;
END_PROGRAM
