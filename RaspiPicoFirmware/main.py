from machine import Pin
from pca9685 import PCA9685
from machine import I2C, Pin
from servo import Servos

#use onboard LED which is controlled by Pin 25
led = Pin(25, Pin.OUT)

sda = Pin(0)
scl = Pin(1)
id = 0
i2c = I2C(id=id, sda=sda, scl=scl)

servo = Servos(i2c=i2c, freq=100)

#initialize at the beginning at 90 / 90

Pan = 90
Tilt = 90

#move Pan Servo 
servo.position(index=0, degrees=Pan)

#move Tilt Servo 
servo.position(index=1, degrees=Tilt)


#set Pan
def pan(pan):
    led.value(1)
    servo.position(index=0, degrees=pan)
    Pan = pan
    print("{""Pan"":"+str(Pan)+",""Tilt"": " + str(Tilt) + "}")
    led.value(0)

#set Tilt
def tilt(tilt):
    led.value(1)
    servo.position(index=1, degrees=tilt)
    Tilt = tilt
    print("{""Pan"":"+str(Pan)+",""Tilt"": " + str(Tilt) + "}")
    led.value(0)


def getStatus():
    print("{""Pan"":"+str(Pan)+",""Tilt"": " + str(Tilt) + "}")