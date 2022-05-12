#grab from https://github.com/matemaciek/mpu9250_CircuitPython/blob/main/mpu9250.py

#########################################
# Author: matemaciek@gmail.com
#########################################
#
# This code handles the i2c communication
# with MPU9250 IMU from CircuitPython.
#
#########################################
#
# based on:
# - https://github.com/makerportal/mpu92-calibration/blob/main/mpu9250_i2c.py
# - https://github.com/wallarug/CircuitPython_MPU9250/blob/master/hybrid/roboticsmasters_mpu9250.py

import time

class IMU:
    """Driver for the MPU9250 9-DoF IMU accelerometer, magnetometer, gyroscope."""

    # Class-level buffers for reading and writing data with the sensor.
    # This reduces memory allocations but means the code is not re-entrant or
    # thread safe!
    _R_BUFFER = bytearray(6)
    _W_BUFFER = bytearray(2)

    def __init__(self, i2c, gyro_indx=0, accel_indx=0, samp_rate_div=0):
        self._bus = i2c
        self._gyro_range = GYRO_CONFIG_VALS[gyro_indx]
        self._accel_range = ACCEL_CONFIG_VALS[accel_indx]
        self._mag_range = MAG_CONFIG_VAL
        self._init_mpu6050(gyro_indx, accel_indx, samp_rate_div)
        self._init_ak8963()
        self._gyr_offset = [0,0,0]
        self._mag_offset = [0,0,0]
        self._acc_offset = [(1<<15, 0),(1<<15, 0),(1<<15, 0)]

    def _init_mpu6050(self, gyro_indx, accel_indx, samp_rate_div):
        # reset all sensors
        self._write_mpu(PWR_MGMT_1, 0x80)
        self._write_mpu(PWR_MGMT_1, 0x00)
        # power management and crystal settings
        self._write_mpu(PWR_MGMT_1, 0x01)
        # alter sample rate (stability)
        # sample rate = 8 kHz/(1+samp_rate_div)
        self._write_mpu(SMPLRT_DIV, samp_rate_div)
        #Write to Configuration register
        self._write_mpu(CONFIG, 0)
        #Write to Gyro configuration register
        self._write_mpu(GYRO_CONFIG, int(GYRO_CONFIG_REGS[gyro_indx]))
        #Write to Accel configuration register
        self._write_mpu(ACCEL_CONFIG, int(ACCEL_CONFIG_REGS[accel_indx]))
        # interrupt register (related to overflow of data [FIFO])
        self._write_mpu(INT_PIN_CFG, 0x22)
        # enable the AK8963 magnetometer in pass-through mode
        self._write_mpu(INT_ENABLE, 1)

    def _init_ak8963(self):
        self._write_ak(AK8963_CNTL, 0x00)
        self._write_ak(AK8963_CNTL, 0x0F)
        coeff_data = self._read_ak(AK8963_ASAX, 3)
        self._mag_coeffs = [(0.5*(coeff_data[i]-128)) / 256.0 + 1.0 for i in range(3)]
        self._write_ak(AK8963_CNTL, 0x00)
        AK8963_bit_res = 0b0001 # 0b0001 = 16-bit
        AK8963_samp_rate = 0b0110 # 0b0010 = 8 Hz, 0b0110 = 100 Hz
        self._write_ak(AK8963_CNTL, (AK8963_bit_res << 4) + AK8963_samp_rate)

    @property
    def raw_acc(self):
        base = self._read_xyz_mpu(ACCEL_OUT)
        offset = self._acc_offset
        return [((1<<15)*base[i]//offset[i][0] + offset[i][1]) for i in range(3)]

    @property
    def raw_gyr(self):
        base = self._read_xyz_mpu(GYRO_OUT)
        offset = self._gyr_offset
        return [base[i] - offset[i] for i in range(3)]

    @property
    def raw_mag(self):
        result_invalid = True
        while result_invalid:
            base = self._read_xyz_ak(MAG_OUT)
            result_invalid = self._read_ak(AK8963_ST2, 1)[0] & 0x80
            if result_invalid:
                print("Magnetometer overflow, retrying.")
            offset = self._mag_offset
        return [base[i] - offset[i] for i in range(3)]

    @property
    def raw_tmp(self):
        return _to_long(self._read_mpu(TEMP_OUT, 2))

    @property
    def acc(self):
        return [(v/(2.0**15.0))*self._accel_range for v in self.raw_acc]

    @property
    def gyr(self):
        return [(v/(2.0**15.0))*self._gyro_range for v in self.raw_gyr]

    @property
    def mag(self):
        return [self._mag_coeffs[i]*(v/(2.0**15.0))*self._mag_range for i, v in enumerate(self.raw_mag)]

    @property
    def tmp(self):
        return (self.raw_tmp / 333.87) + 21.0
    
    def cal_acc(self, data):
        result = {}
        for wall, values in data.items():
            samples = len(values)
            result[wall] = [sum(l)//samples for l in zip(*values)]
        return result
    
    def cal_gyr(self, data):
        samples = len(data)
        return [sum(l)//samples for l in zip(*data)]
    
    def cal_mag(self, data):
        result = {}
        for axis, values in data.items():
            result[axis] = [(max(l)+min(l))//2 for l in zip(*values)]
        return result
    
    def calibrate(self, data):
        self._gyr_offset = data["gyr"]
        self._mag_offset = [(data["mag"][ax][ai]+data["mag"][bx][bi])//2 for (ax, ai, bx, bi) in [('y', 0, 'z', 0), ('x', 0, 'z', 1), ('x', 1, 'y', 1)]]
        extremes = [(min(data["acc"][axis+'-']), max(data["acc"][axis+'+'])) for axis in ['x', 'y', 'z']]
        self._acc_offset = [(ma-mi, (1<<14)*(ma+mi)//(mi-ma)) for (mi,ma) in extremes]

    def _write_mpu(self, address, val):
        self._write(MPU6050_ADDR, address, val)

    def _write_ak(self, address, val):
        self._write(AK8963_ADDR, address, val)

    def _read_mpu(self, address, count):
        return self._read(MPU6050_ADDR, address, count)

    def _read_ak(self, address, count):
        return self._read(AK8963_ADDR, address, count)

    def _read_xyz_mpu(self, address):
        return self._read_xyz(MPU6050_ADDR, address, False)

    def _read_xyz_ak(self, address):
        (x,y,z) = self._read_xyz(AK8963_ADDR, address, True)
        return (y,x,-z)

    def _write(self, device, address, val):
        self._W_BUFFER[0] = address & 0xFF
        self._W_BUFFER[1] = val & 0xFF
        while not self._bus.try_lock():
            pass
        self._bus.writeto(device, self._W_BUFFER)
        self._bus.unlock()
        time.sleep(0.01)

    def _read(self, device, address, count):
        buffer = self._R_BUFFER if count == 6 else bytearray(count)
        buffer[0] = address & 0xFF
        while not self._bus.try_lock():
            pass
        self._bus.writeto_then_readfrom(device, buffer, buffer, out_end=1)
        self._bus.unlock()
        return buffer

    def _read_xyz(self, device, address, swap):
        bytes = self._read(device, address, 6)
        return [_to_long(bytes, 2*i, swap) for i in range(3)]

def _to_long(buffer, index=0, swap=False):
    # combine high and low for unsigned bit value
    value = ((buffer[index+swap] << 8) | buffer[index+(not swap)])
    # convert to signed value
    if(value > 32768):
        value -= 65536
    return value

# MPU6050 Registers
MPU6050_ADDR = 0x68
PWR_MGMT_1   = 0x6B
SMPLRT_DIV   = 0x19
CONFIG       = 0x1A
GYRO_CONFIG  = 0x1B
ACCEL_CONFIG = 0x1C
INT_PIN_CFG  = 0x37
INT_ENABLE   = 0x38
ACCEL_OUT    = 0x3B
TEMP_OUT     = 0x41
GYRO_OUT     = 0x43

#AK8963 registers
AK8963_ADDR  = 0x0C
MAG_OUT      = 0x03
AK8963_ST2   = 0x09
AK8963_CNTL  = 0x0A
AK8963_ASAX  = 0x10

GYRO_CONFIG_REGS = [0b00000, 0b01000, 0b10000, 0b11000] # byte registers
GYRO_CONFIG_VALS = [250.0, 500.0, 1000.0, 2000.0] # degrees/sec
ACCEL_CONFIG_REGS = [0b00000, 0b01000, 0b10000, 0b11000] # byte registers
ACCEL_CONFIG_VALS = [2.0, 4.0, 8.0, 16.0] # g (g = 9.81 m/s^2)
MAG_CONFIG_VAL = 4800.0 # magnetometer sensitivity: 4800 uT
