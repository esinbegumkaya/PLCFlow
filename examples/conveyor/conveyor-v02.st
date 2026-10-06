PROGRAM ConveyorControl
VAR
    StartButton AT %I0.0 : BOOL;
    StopButton  AT %I0.1 : BOOL;
    Motor       AT %Q0.0 : BOOL;
    StartDelay : TON;
    Counter : INT;
END_VAR

StartDelay(IN := StartButton AND NOT StopButton, PT := T#100ms);
Motor := StartDelay.Q;

IF Motor THEN
    Counter := Counter + 1;
END_IF;
END_PROGRAM
