#define PWM_FREQ 38000 // in Hertz (SET YOUR FREQUENCY)
uint16_t TIM_ARR = (uint16_t)(24000000 / PWM_FREQ) - 1; // Don't change! Calc's period.

int led = D0;

int COOL = 0x0;
int FAN = 0x2;
int MONEY_SAVER = 0x6;
int DRY = 0x1;

int modes[] = {COOL, MONEY_SAVER, FAN, DRY};
int speeds[] = {0x0, 0x2, 0x4};

int mode = 0;
int temp = 60;
int speed = 0;
int on = 0;

void setup() {
    Spark.function("setTemp", setTemp);
    Spark.function("changeSpeed", changeSpeed);
    Spark.function("power", power);
    Spark.function("changeMode", changeMode);

    Spark.variable("on", &on, INT);
    Spark.variable("temp", &temp, INT);
    Spark.variable("fanspeed", &speed, INT);
    Spark.variable("mode", &mode, INT);

    pinMode(led, OUTPUT);
    analogWrite2(led, 0);
}

void loop() {
   // nothing
}

int power(String command) {
  if (command == "ON") turnOn();
  else turnOff();
  return 1;
}

int setTemp(String command) {
    int value = command.toInt();
    if (value < 60 || value > 86) return -1;
    temp = value;
    update();
    return 1;
}

int changeMode(String command) {
    mode = command.toInt();
    update();
    return 1;
}

int changeSpeed(String command) {
    speed = command.toInt();
    update();
}

void update() {
    int data = 0x8820000;
    // always act like it is off, so this will turn it on
    // data |= (on & 0x1) << 15;
    // add mode
    data |= (modes[mode] & 0x7) << 12;
    // add temp
    int tv = temp - 59;
    if (tv < 16) data |= (tv & 0xF) << 8;
    else {
        tv = tv - 16;
        data |= (tv & 0xF) << 8;
        data |= 0x1 << 7;
    }
    data |= (speeds[speed] & 0x7) << 4;

    int checksum = 0;
    for (int i = 4; i < 28; i += 4) {
        checksum += (data & (0xF << i)) >> i;
    }
    data |= checksum & 0xF;
    sendData(data, 28);
    // if it wasn't on, it is now
    on = 1;
}

void turnOn() {
    update();
    on = 1;
}

void turnOff() {
    on = 0;
    sendData(0x88c0051, 28);
}

void sendData(unsigned long data, int nbits) {
    noInterrupts();
    mark(8553);
    space(4134);
    for (int i = nbits - 1; i >= 0; i--) {
        if (data & (1 << i)) {
            mark(660);
            space(1522);
        } else {
            mark(660);
            space(432);
        }
    }
    mark(660);
    space(0);
    interrupts();
}

void mark(unsigned int time) {
    analogWrite2(led, 85);
    delayMicroseconds(time);
}

void space(unsigned int time) {
    analogWrite2(led, 0);
    delayMicroseconds(time);
}

void analogWrite2(uint16_t pin, uint8_t value) {
  TIM_OCInitTypeDef TIM_OCInitStructure;

  if (pin >= TOTAL_PINS || PIN_MAP[pin].timer_peripheral == NULL) {
    return;
  }
  // SPI safety check
  if (SPI.isEnabled() == true && (pin == SCK || pin == MOSI || pin == MISO)) {
    return;
  }
  // I2C safety check
  if (Wire.isEnabled() == true && (pin == SCL || pin == SDA)) {
    return;
  }
  // Serial1 safety check
  if (Serial1.isEnabled() == true && (pin == RX || pin == TX)) {
    return;
  }
  if (PIN_MAP[pin].pin_mode != OUTPUT && PIN_MAP[pin].pin_mode != AF_OUTPUT_PUSHPULL) {
    return;
  }
  // Don't re-init PWM and cause a glitch if already setup, just update duty cycle and return.
  if (PIN_MAP[pin].pin_mode == AF_OUTPUT_PUSHPULL) {
    TIM_OCInitStructure.TIM_Pulse = (uint16_t)(value * (TIM_ARR + 1) / 255);
    if (PIN_MAP[pin].timer_ch == TIM_Channel_1) {
      PIN_MAP[pin].timer_peripheral-> CCR1 = TIM_OCInitStructure.TIM_Pulse;
    } else if (PIN_MAP[pin].timer_ch == TIM_Channel_2) {
      PIN_MAP[pin].timer_peripheral-> CCR2 = TIM_OCInitStructure.TIM_Pulse;
    } else if (PIN_MAP[pin].timer_ch == TIM_Channel_3) {
      PIN_MAP[pin].timer_peripheral-> CCR3 = TIM_OCInitStructure.TIM_Pulse;
    } else if (PIN_MAP[pin].timer_ch == TIM_Channel_4) {
      PIN_MAP[pin].timer_peripheral-> CCR4 = TIM_OCInitStructure.TIM_Pulse;
    }
    return;
  }

  TIM_TimeBaseInitTypeDef TIM_TimeBaseStructure;

  //PWM Frequency : PWM_FREQ (Hz)
  uint16_t TIM_Prescaler = (uint16_t)(SystemCoreClock / 24000000) - 1; //TIM Counter clock = 24MHz

  // TIM Channel Duty Cycle(%) = (TIM_CCR / TIM_ARR + 1) * 100
  uint16_t TIM_CCR = (uint16_t)(value * (TIM_ARR + 1) / 255);

  // AFIO clock enable
  RCC_APB2PeriphClockCmd(RCC_APB2Periph_AFIO, ENABLE);

  pinMode(pin, AF_OUTPUT_PUSHPULL);

  // TIM clock enable
  if (PIN_MAP[pin].timer_peripheral == TIM2)
    RCC_APB1PeriphClockCmd(RCC_APB1Periph_TIM2, ENABLE);
  else if (PIN_MAP[pin].timer_peripheral == TIM3)
    RCC_APB1PeriphClockCmd(RCC_APB1Periph_TIM3, ENABLE);
  else if (PIN_MAP[pin].timer_peripheral == TIM4)
    RCC_APB1PeriphClockCmd(RCC_APB1Periph_TIM4, ENABLE);

  // Time base configuration
  TIM_TimeBaseStructure.TIM_Period = TIM_ARR;
  TIM_TimeBaseStructure.TIM_Prescaler = TIM_Prescaler;
  TIM_TimeBaseStructure.TIM_ClockDivision = 0;
  TIM_TimeBaseStructure.TIM_CounterMode = TIM_CounterMode_Up;

  TIM_TimeBaseInit(PIN_MAP[pin].timer_peripheral, & TIM_TimeBaseStructure);

  // PWM1 Mode configuration
  TIM_OCInitStructure.TIM_OCMode = TIM_OCMode_PWM1;
  TIM_OCInitStructure.TIM_OutputState = TIM_OutputState_Enable;
  TIM_OCInitStructure.TIM_OCPolarity = TIM_OCPolarity_High;
  TIM_OCInitStructure.TIM_Pulse = TIM_CCR;

  if (PIN_MAP[pin].timer_ch == TIM_Channel_1) {
    // PWM1 Mode configuration: Channel1
    TIM_OC1Init(PIN_MAP[pin].timer_peripheral, & TIM_OCInitStructure);
    TIM_OC1PreloadConfig(PIN_MAP[pin].timer_peripheral, TIM_OCPreload_Enable);
  } else if (PIN_MAP[pin].timer_ch == TIM_Channel_2) {
    // PWM1 Mode configuration: Channel2
    TIM_OC2Init(PIN_MAP[pin].timer_peripheral, & TIM_OCInitStructure);
    TIM_OC2PreloadConfig(PIN_MAP[pin].timer_peripheral, TIM_OCPreload_Enable);
  } else if (PIN_MAP[pin].timer_ch == TIM_Channel_3) {
    // PWM1 Mode configuration: Channel3
    TIM_OC3Init(PIN_MAP[pin].timer_peripheral, & TIM_OCInitStructure);
    TIM_OC3PreloadConfig(PIN_MAP[pin].timer_peripheral, TIM_OCPreload_Enable);
  } else if (PIN_MAP[pin].timer_ch == TIM_Channel_4) {
    // PWM1 Mode configuration: Channel4
    TIM_OC4Init(PIN_MAP[pin].timer_peripheral, & TIM_OCInitStructure);
    TIM_OC4PreloadConfig(PIN_MAP[pin].timer_peripheral, TIM_OCPreload_Enable);
  }

  TIM_ARRPreloadConfig(PIN_MAP[pin].timer_peripheral, ENABLE);

  // TIM enable counter
  TIM_Cmd(PIN_MAP[pin].timer_peripheral, ENABLE);
}
