; ModuleID = 'ConveyorControl'
source_filename = "ConveyorControl.st"
target triple = "wasm32-unknown-unknown"

@plc_cycle_ms = global i32 10
@StartButton = global i1 0
@StopButton = global i1 0
@Motor = global i1 0
@StartDelay_IN = global i1 0
@StartDelay_Q = global i1 0
@StartDelay_ET = global i32 0
@StartDelay_PT = global i32 0
@Counter = global i32 0

define void @plc_set_cycle_ms(i32 %value) {
entry:
  store i32 %value, ptr @plc_cycle_ms
  ret void
}

define i32 @plc_get_cycle_ms() {
entry:
  %value = load i32, ptr @plc_cycle_ms
  ret i32 %value
}

define void @plc_set_StartButton(i32 %value) {
entry:
  %normalized = icmp ne i32 %value, 0
  store i1 %normalized, ptr @StartButton
  ret void
}

define i32 @plc_get_StartButton() {
entry:
  %raw = load i1, ptr @StartButton
  %value = zext i1 %raw to i32
  ret i32 %value
}

define void @plc_set_StopButton(i32 %value) {
entry:
  %normalized = icmp ne i32 %value, 0
  store i1 %normalized, ptr @StopButton
  ret void
}

define i32 @plc_get_StopButton() {
entry:
  %raw = load i1, ptr @StopButton
  %value = zext i1 %raw to i32
  ret i32 %value
}

define i32 @plc_get_Motor() {
entry:
  %raw = load i1, ptr @Motor
  %value = zext i1 %raw to i32
  ret i32 %value
}

define i32 @plc_get_StartDelay_Q() {
entry:
  %raw = load i1, ptr @StartDelay_Q
  %value = zext i1 %raw to i32
  ret i32 %value
}

define i32 @plc_get_StartDelay_ET() {
entry:
  %value = load i32, ptr @StartDelay_ET
  ret i32 %value
}

define i32 @plc_get_StartDelay_PT() {
entry:
  %value = load i32, ptr @StartDelay_PT
  ret i32 %value
}

define i32 @plc_get_Counter() {
entry:
  %value = load i32, ptr @Counter
  ret i32 %value
}

define void @scan_cycle() {
entry:
  %t1 = load i1, ptr @StartButton
  %t2 = load i1, ptr @StopButton
  %t3 = xor i1 %t2, true
  %t4 = and i1 %t1, %t3
  store i1 %t4, ptr @StartDelay_IN
  store i32 100, ptr @StartDelay_PT
  %t5 = load i32, ptr @StartDelay_ET
  %t6 = load i32, ptr @plc_cycle_ms
  %t7 = add i32 %t5, %t6
  %t8 = icmp sgt i32 %t7, 100
  %t9 = select i1 %t8, i32 100, i32 %t7
  %t10 = select i1 %t4, i32 %t9, i32 0
  store i32 %t10, ptr @StartDelay_ET
  %t11 = icmp sge i32 %t10, 100
  %t12 = and i1 %t4, %t11
  store i1 %t12, ptr @StartDelay_Q
  %t13 = load i1, ptr @StartDelay_Q
  store i1 %t13, ptr @Motor
  %t14 = load i1, ptr @Motor
  br i1 %t14, label %if_then_1, label %if_else_2
if_then_1:
  %t15 = load i32, ptr @Counter
  %t16 = add i32 %t15, 1
  store i32 %t16, ptr @Counter
  br label %if_end_3
if_else_2:
  br label %if_end_3
if_end_3:
  ret void
}
