// Raiz 1.0 — Arduino Uno, sem DHT e sem bibliotecas externas.
// Antes de alimentar: remova o DHT danificado e confira a montagem.
// LDR: 5V -> LDR -> A0 -> resistor 10 kohms -> GND.
// Solo: VCC -> 3.3V, GND -> GND, AOUT -> A1.
// Use os nomes impressos nos componentes, nao apenas cores de fios.

const byte PINO_LUZ = A0;
const byte PINO_SOLO = A1;
unsigned long sequencia = 0;

int mediaAnalogica(byte pino) {
  analogRead(pino); // descarta a primeira leitura apos trocar o canal
  long soma = 0;
  for (byte i = 0; i < 8; i++) {
    soma += analogRead(pino);
    delay(2);
  }
  return (soma + 4) / 8;
}

void setup() {
  Serial.begin(9600);
}

void loop() {
  int luz = mediaAnalogica(PINO_LUZ);
  int solo = mediaAnalogica(PINO_SOLO);
  Serial.print(F("{\"type\":\"sensors\",\"seq\":"));
  Serial.print(sequencia++);
  Serial.print(F(",\"light_raw\":"));
  Serial.print(luz);
  Serial.print(F(",\"soil_raw\":"));
  Serial.print(solo);
  Serial.print(F(",\"uptime_ms\":"));
  Serial.print(millis());
  Serial.println(F("}"));
  delay(2500);
}
