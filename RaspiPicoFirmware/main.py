from machine import Pin
from pca9685 import PCA9685
from machine import I2C, Pin
from servo import Servos
from mpu9250 import MPU9250
import time

#use onboard LED which is controlled by Pin 25
led = Pin(25, Pin.OUT)

sda = Pin(0)
scl = Pin(1)
id = 0
i2c = I2C(id=id, sda=sda, scl=scl)

servo = Servos(i2c=i2c, freq=100)

imu = MPU9250(i2c)

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
    global Pan
    Pan = pan    
    led.value(0)
    time.sleep(1)
    getStatus()

#set Tilt
def tilt(tilt):
    led.value(1)
    servo.position(index=1, degrees=tilt)
    global Tilt
    Tilt = tilt    
    led.value(0)
    time.sleep(1)
    getStatus()

#set Both
def panTilt(pan,tilt):
    led.value(1)
    servo.position(index=0, degrees=pan)
    global Pan
    Pan = pan
    servo.position(index=1, degrees=tilt)
    global Tilt
    Tilt = tilt    
    led.value(0)
    time.sleep(1)
    getStatus()

def getStatus():
    global Pan
    global Tilt
    ax=round(imu.accel.x,2)
    ay=round(imu.accel.y,2)
    az=round(imu.accel.z,2)
    mx=round(imu.mag.x,2)
    my=round(imu.mag.y,2)  
    mz=round(imu.mag.z,2)
    
    print("{""pan"":"+str(Pan)+",""tilt"":" + str(Tilt) + ",""ax"":"+str(ax)+",""ay"":"+str(ay)+",""az"":" +str(az)+",""mx"":" + str(mx)+",""my"":" + str(my)+",""mz"":" + str(mz) + "}")
    
#next step will be to use this library to get YAW  as well 
#https://github.com/thisisG/MPU6050-I2C-Python-Class