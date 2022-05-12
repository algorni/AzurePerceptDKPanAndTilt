'''
IMPORTS
'''
import time
import board
import digitalio
from busio import I2C
import time
import usb_cdc
import gc
from adafruit_servokit import ServoKit
import mpu9250
import json

#define LED onboard
led = digitalio.DigitalInOut(board.LED)
led.direction = digitalio.Direction.OUTPUT
led.value = False

'''
CONSTANTS
'''
DEBUG = False

expectedPan = 90
expectedTilt = 90

'''
FUNCTIONS
'''
def log(msg):
    if DEBUG: print(msg)

def reportDirection(imu, serial):
    log("Reporting Direction")
    try:   
        reportedDirectionObj = {"messageType":"reportDirection", "expectedPan":expectedPan, "expectedTilt":expectedTilt, "acc": imu.acc, "mag": imu.mag}    
        reportedDirectionStr = f"{json.dumps(reportedDirectionObj)}\n"
        reportedDirectionBuf = reportedDirectionStr.encode("utf-8")    
        serial.write(reportedDirectionBuf)
        log(reportedDirectionBuf)
    except Exception as ex :
        reportError(serial, f"An error occurred reporting Direction: {ex}")
    
    
def reportError(serial, msg):
    log(f"Reporting an error {msg}")
    responseObj = {"messageType":"errorReporting", "errorMessage": msg}
    led.value = True
    time.sleep(0.6)
    led.value = False
    try:
        responseObjStr = f"{json.dumps(responseObj)}\n"
        responseObjBuf = responseObjStr.encode("utf-8")  
        serial.write(responseObjBuf)        
    except Exception:
        #put led on the led..
        led.value = True
        pass

'''
RUNTIME START
'''
if __name__ == '__main__':
    log("Program Started")
    
    # Get the USB data feed object
    serial = usb_cdc.data
    serial.timeout = 2        
    log("Serial Data port init done")
        
    # Set up I2C 
    i2c_bus = I2C(board.GP1, board.GP0)
    
    #Setup the Servo Driver
    kit = ServoKit(channels=16, i2c=i2c_bus, address=0x40, frequency=50)
           
    panServo = kit.servo[0]
    tiltServo = kit.servo[1]
        
    #set initial position at boot
    panServo.angle = expectedPan  
    tiltServo.angle = expectedTilt  
    
    time.sleep(0.500); 
    
    log("Servo i2c init done")

    #Setup the IMU Driver    
    imu = mpu9250.IMU(i2c_bus)
    
    log("MPU i2c init done")

    loopIndex = 0

    #Start main loop waiting for serial commands
    while True:
        
        loopDivider = 5
        loopIndex = loopIndex + 1
        
        if DEBUG: 
            led.value = False
            time.sleep(0.2)
            led.value = True                
            time.sleep(0.2)                 
        else:
            time.sleep(0.1)                 
            loopDivider = 20
                 
        #report the position over a period of time...      
        reportLoop = loopIndex % loopDivider
        if (reportLoop == 0):                         
            reportDirection(imu,serial)               
                 
                                        
        try:                                
            # Check for incoming data
            if serial.in_waiting > 0:
                log("Receiving Message")
                
                if DEBUG: 
                    #extend a bit the led to signale it received something from serial
                    time.sleep(0.5)
                        
                receivedBytes = serial.readline()                                                
                decodedData = receivedBytes.decode("utf-8")
                
                #parse the received line as JSON            
                parsedData =json.loads(decodedData)            
                
                log(f'Received and deserialized that Message: \n{decodedData}')               
                
                #checking for the command
                messageType = parsedData["messageType"]            
                log(f"Received the following message type: {messageType}")
                
                if (messageType == "setDirection"):
                    expectedPan = parsedData["expectedPan"]                    
                    expectedTilt = parsedData["expectedTilt"]
                    panServo.angle = expectedPan
                    tiltServo.angle = expectedTilt                                   
                    reportDirection(imu,serial)
                    
        except Exception as ex :
            reportError(serial,f"An error occurred while receiving a message from serial: {ex}")
            pass
