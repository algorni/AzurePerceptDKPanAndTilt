# AzurePerceptDKPanAndTilt
PerceptDK Camera Sensor has a great Field of View (120° diagonal) but what if you want to see beyond this range?  An option could be to host multiple cameras to cover a wider angle or just let it move!


In this git hub repo you will find a coupel of custom IoT Edge module to control 2 servo which i've used to build a Pan & Tilt mechanics with a simple 3D printer.
The servo are directly controlled by a Raspberry Pi Pico board connected to the Percept Carrier Board and orchestrated by those IoT Edge module.

Unfortunately a detailed docs around this sample is WIP, I will update the repo soon!

## Overall Idea

The overall ideas is having the Pan & Tilt servo connected to the rapi pico with a servo control board. 
The Raspi Pi runs CictuitPython and the Code is in this repo.   I've also an accelerometer & magnetometer sensor (i just grab the value and let surface up to the cloud as Twin values but not actually used in the control loop yet).
I'm using the Serial Data line over USB (CircuitPython is way better than MicroPython on doing that..) to exchange JSON messages from the Carrier Board to the Raspi Pico.


On the Carrier Board i've a pan & tilt controller module which is doing this data exchange with the pico.   You can control the pan & tilt via Module Twins updates or via local edgeHub messages (the module has input and output ports for that purpose)


Then i've another module that listen to the Percept Eye vision AI output, find the tracked object (you can choose one label and the module will look at the first label found) calculate the center position and send a command to the pan & tilt module to move the percept eye to center to this object.


It basically track the object in the scene!

