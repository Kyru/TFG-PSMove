/**
 * UniMove API - A Unity plugin for the PlayStation Move motion controller
 * Copyright (C) 2012, 2013, Copenhagen Game Collective (http://www.cphgc.org)
 * 					         Patrick Jarnfelt
 * 					         Douglas Wilson (http://www.doougle.net)
 * 
 * 
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *    1. Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *
 *    2. Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 **/

/**
 * IMPORTANT NOTES!
 *
 * -- This API has been compiled for Mac OSX (10.7 and later) specifically.
 * 
 * -- This API assumes that the controller has already been paired and connected by Bluetooth beforehand.
 *    To pair a controller(s), use the Pairing Utility provided by the PS Move API http://thp.io/2010/psmove/.
 *    To connect a controller by Bluetooth, just press the PS button after pairing it.
 *    You can also use the controllers by USB, but with limited functionality (see below).
 * 
 * -- Features include:
 * 
 * 	- Setting the RGB LED color and rumble intensity (USB and Bluetooth)
 * 	- Read the status of the digital buttons (Bluetooth only)
 * 	- Read the status of the analog trigger (Bluetooth only)
 * 	- Read values for the internal sensors (Bluetooth only):
 *     - accelorometer
 *     - gyroscope
 *     - magnetometer
 *     - temperature
 *     - battery level
 * 
 * Please see the README for more information!
 **/

using System;
using UnityEngine;
using System.Runtime.InteropServices;

#region enums and structs

public enum PSMoveTrackerStatus
{
    Tracker_NOT_CALIBRATED, /*!< Controller not registered with tracker */
    Tracker_CALIBRATION_ERROR, /*!< Calibration failed (check lighting, visibility) */
    Tracker_CALIBRATED, /*!< Color calibration successful, not currently tracking */
    Tracker_TRACKING, /*!< Calibrated and successfully tracked in the camera */
};

/// <summary>
/// The Move controller can be connected by USB and/or Bluetooth.
/// </summary>
public enum PSMoveConnectionType
{
    Bluetooth,
    USB,
    Unknown,
};

public enum PSMove_Bool { PSMove_False = 0, PSMove_True = 1 }

// Not entirely sure why some of these buttons (R3/L3) are exposed...
public enum PSMoveButton
{
    L2 = 1 << 0x00,
    R2 = 1 << 0x01,
    L1 = 1 << 0x02,
    R1 = 1 << 0x03,
    Triangle = 1 << 0x04,
    Circle = 1 << 0x05,
    Cross = 1 << 0x06,
    Square = 1 << 0x07,
    Select = 1 << 0x08,
    L3 = 1 << 0x09,
    R3 = 1 << 0x0A,
    Start = 1 << 0x0B,
    Up = 1 << 0x0C,
    Right = 1 << 0x0D,
    Down = 1 << 0x0E,
    Left = 1 << 0x0F,
    PS = 1 << 0x10,
    Move = 1 << 0x13,
    Trigger = 1 << 0x14,	/* We can use this value with IsButtonDown() (or the events) to get 
							 * a binary yes/no answer about if the trigger button is down at all.
							 * For the full integer/analog value of the trigger, see the corresponding property below.
							 */
};

// Used by psmove_get_battery().
public enum PSMove_Battery_Level
{
    Batt_MIN = 0x00, /*!< Battery is almost empty (< 20%) */
    Batt_20Percent = 0x01, /*!< Battery has at least 20% remaining */
    Batt_40Percent = 0x02, /*!< Battery has at least 40% remaining */
    Batt_60Percent = 0x03, /*!< Battery has at least 60% remaining */
    Batt_80Percent = 0x04, /*!< Battery has at least 80% remaining */
    Batt_MAX = 0x05, /*!< Battery is fully charged (not on charger) */
    Batt_CHARGING = 0xEE, /*!< Battery is currently being charged */
    Batt_CHARGING_DONE = 0xEF, /*!< Battery is fully charged (on charger) */
};

public enum PSMove_Frame
{
    Frame_FirstHalf = 0, /*!< The older frame */
    Frame_SecondHalf, /*!< The most recent frame */
};

public enum PSMoveTracker_Status
{
    Tracker_NOT_CALIBRATED, /*!< Controller not registered with tracker */
    Tracker_CALIBRATION_ERROR, /*!< Calibration failed (check lighting, visibility) */
    Tracker_CALIBRATED, /*!< Color calibration successful, not currently tracking */
    Tracker_TRACKING, /*!< Calibrated and successfully tracked in the camera */
};

public enum PSMoveTracker_Exposure
{
    Exposure_LOW, /*!< Very low exposure: Good tracking, no environment visible */
    Exposure_MEDIUM, /*!< Middle ground: Good tracking, environment visibile */
    Exposure_HIGH, /*!< High exposure: Fair tracking, but good environment */
    Exposure_INVALID, /*!< Invalid exposure value (for returning failures) */
};

public enum PSMove_LED_Auto_Option
{
    PSMove_LED_Auto_On,
    PSMove_LED_Auto_Off
};

public enum PSMove_Connect_Status
{
    MoveConnect_OK,
    MoveConnect_Error,
    MoveConnect_NoData,
    MoveConnect_Unknown
}

public class UniMoveButtonEventArgs : EventArgs
{
    public readonly PSMoveButton button;

    public UniMoveButtonEventArgs(PSMoveButton button)
    {
        this.button = button;
    }
}

#endregion

public class UniMoveController : MonoBehaviour
{
    #region private instance variables

    /// <summary>
    /// The handle for this controller. This pointer is what the psmove library uses for reading data via the hid library.
    /// </summary>
    private IntPtr handle;
    private IntPtr tracker;
    private IntPtr fusion;
    private bool disconnected = false;

    private float timeElapsed = 0.0f;
    private float updateRate = 0.05f;   // The default update rate is 50 milliseconds

    private static float MIN_UPDATE_RATE = 0.02f; // You probably don't want to update the controller more frequently than every 20 milliseconds

    private float trigger = 0f;
    private uint currentButtons = 0;
    private uint prevButtons = 0;

    private Vector3 rawAccel = Vector3.down;
    private Vector3 accel = Vector3.down;
    private Vector3 magnet = Vector3.zero;
    private Vector3 rawGyro = Vector3.zero;
    private Vector3 gyro = Vector3.zero;
    private Quaternion orientation = Quaternion.identity;
    private Vector3 position = Vector3.zero;


    // TODO: These values still need to be implemented, so we don't expose them publicly
    private PSMove_Battery_Level battery = PSMove_Battery_Level.Batt_20Percent;
    private float temperature = 0f;

    // <F> Variable para especificar la posición de la cámara para los cálculos necesarios en ProcessData()
    private string cameraPosition;

    // <F> ProcessData() limiter
    private int processDataLimiter = 0;

    /// <summary>
    /// Event fired when the controller disconnects unexpectedly (i.e. on going out of range).
    /// </summary>
    public event EventHandler OnControllerDisconnected;

    #endregion
    /// <summary>
    /// Returns whether the connecting succeeded or not.
    /// 
    /// NOTE! This function does NOT pair the controller by Bluetooth.
    /// If the controller is not already paired, it can only be connected by USB.
    /// See README for more information.
    /// </summary>
    public bool Init(int index)
    {

        handle = psmove_connect_by_id(index);

        Debug.Log("index " + index + " handle: " + handle);

        // Error check the result!
        if (handle == IntPtr.Zero) return false;

        tracker = psmove_tracker_new();
        psmove_tracker_set_exposure(tracker, PSMoveTracker_Exposure.Exposure_LOW);		//<F>
        fusion = psmove_fusion_new(tracker, 1.0f, 1000.0f);
        //psmove_tracker_set_mirror(tracker, 1);

        if (index == 0)
        {
            while (psmove_tracker_enable_with_color(tracker, handle, 255, 0, 255) != PSMoveTracker_Status.Tracker_CALIBRATED) ;
        }
        else
        {
            while (psmove_tracker_enable_with_color(tracker, handle, 0, 255, 255) != PSMoveTracker_Status.Tracker_CALIBRATED) ;
        }
        //while (psmove_tracker_enable(tracker, handle) != PSMoveTrackerStatus.Tracker_CALIBRATED) ;

        psmove_enable_orientation(handle, 1);

        Debug.Log("PS Move has calibration: " + psmove_has_calibration(handle));
        Debug.Log("PS Move has orientation: " + psmove_has_orientation(handle));

        byte r = 0, g = 0, b = 0;
        psmove_tracker_get_color(tracker, handle, ref r, ref g, ref b);

        Color newColor = new Color(r, g, b);
        Debug.Log("index: " + index + " ,color " + newColor);

        psmove_reset_orientation(handle);

        // Make sure the connection is actually sending data. If not, this is probably a controller 
        // you need to remove manually from the OSX Bluetooth Control Panel, then re-connect.
        return (psmove_update_leds(handle) != 0);
    }

    // <F> 
    public string CameraPosition
    {
        get { return cameraPosition; }
        set { cameraPosition = value; }
    }


    /// <summary>
    /// Static function that returns the number of *all* controller connections.
    /// This count will tally both USB and Bluetooth connections.
    /// Note that one physical controller, then, might register multiple connections.
    /// To discern between different connection types, see the ConnectionType property below.
    /// </summary>
    public static int GetNumConnected()
    {
        return psmove_count_connected();
    }

    /// <summary>
    /// The amount of time, in seconds, between update calls.
    /// The faster this rate, the more responsive the controllers will be.
    /// However, update too fast and your computer won't be able to keep up (see below).
    /// You almost certainly don't want to make this faster than 20 milliseconds (0.02f).
    /// 
    /// NOTE! We find that slower/older computers can have trouble keeping up with a fast update rate,
    /// especially the more controllers that are connected. See the README for more information.
    /// </summary>
    public float UpdateRate
    {
        get { return this.updateRate; }
        set { updateRate = Math.Max(value, MIN_UPDATE_RATE); }  // Clamp negative values up to 0
    }

    void Update()
    {
        if (disconnected) return;

        // we want to update the previous buttons outside the update restriction so,
        // we only get one button event pr. unity update frame
        prevButtons = currentButtons;

        timeElapsed += Time.deltaTime;      // <F> Esto estaba comentado antes


        // Here we manually enforce updates only every updateRate amount of time
        // The reason we don't just do this in FixedUpdate is so the main program's FixedUpdate rate 
        // can be set independently of the controllers' update rate.

        if (timeElapsed < updateRate) return;       // <F> Esto estaba conectado antes
        else timeElapsed = 0.0f;                    // <F> Esto estaba conectado antes

        uint buttons = 0;

        // NOTE! There is potentially data waiting in queue. 
        // We need to poll *all* of it by calling psmove_poll() until the queue is empty. Otherwise, data might begin to build up.
        while (psmove_poll(handle) > 0)
        {
            // We are interested in every button press between the last update and this one:
            buttons = buttons | psmove_get_buttons(handle);

            // The events are not really working from the PS Move Api. So we do our own with the prevButtons
            //psmove_get_button_events(handle, ref pressed, ref released);
        }
        currentButtons = buttons;

        // <F> Test to limit the frames and improve performance, if statemente should be active in 2 players.
        //if (processDataLimiter % 2 == 0)
        //{
            psmove_tracker_update_image(tracker);
            psmove_tracker_update(tracker, handle);
        //}

        //psmove_tracker_update_image(tracker);
        //psmove_tracker_update(tracker, handle);

        ProcessData();


        // Send a report to the controller to update the LEDs and rumble.
        if (psmove_update_leds(handle) == 0)
        {
            // If it returns zero, the controller must have disconnected (i.e. out of battery or out of range),
            // so we should fire off any events and disconnect it.
            OnControllerDisconnected(this, new EventArgs());
            Disconnect();
        }

        processDataLimiter++;
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    /// <summary>
    /// Returns true if "button" is currently down.
    /// </summary
    public bool GetButton(PSMoveButton b)
    {
        if (disconnected) return false;

        return ((currentButtons & (uint)b) != 0);
    }

    /// <summary>
    /// Returns true if "button" is pressed down this instant.
    /// </summary
    public bool GetButtonDown(PSMoveButton b)
    {
        if (disconnected) return false;
        return ((prevButtons & (uint)b) == 0) && ((currentButtons & (uint)b) != 0);
    }

    /// <summary>
    /// Returns true if "button" is released this instant.
    /// </summary
    public bool GetButtonUp(PSMoveButton b)
    {
        if (disconnected) return false;

        return ((prevButtons & (uint)b) != 0) && ((currentButtons & (uint)b) == 0);
    }
    /// <summary>
    /// Disconnect the controller
    /// </summary>

    public void Disconnect()
    {
        disconnected = true;
        SetLED(0, 0, 0);
        SetRumble(0);
        psmove_fusion_free(fusion);
        psmove_tracker_free(tracker);
        psmove_disconnect(handle);
    }

    /// <summary>
    /// Whether or not the controller has been disconnected
    /// </summary
    public bool Disconnected
    {
        get { return disconnected; }
    }

    /// <summary>
    /// Sets the amount of rumble
    /// </summary>
    /// <param name="rumble">the rumble amount (0-1)</param>
    public void SetRumble(float rumble)
    {
        if (disconnected) return;

        // Clamp value between 0 and 1:
        byte rumbleByte = (byte)(Math.Min(Math.Max(rumble, 0f), 1f) * 255);

        psmove_set_rumble(handle, (char)rumbleByte);
    }

    /// <summary>
    /// Sets the LED color
    /// </summary>
    /// <param name="color">Unity's Color type</param>
    public void SetLED(Color color)
    {
        if (disconnected) return;

        psmove_set_leds(handle, (char)(color.r * 255), (char)(color.g * 255), (char)(color.b * 255));
        psmove_update_leds(handle);
    }

    public void ResetOrientation()
    {
        psmove_reset_orientation(handle);
    }

    /// <summary>
    /// Sets the LED color
    /// </summary>
    /// <param name="r">Red value of the LED color (0-255)</param>
    /// <param name="g">Green value of the LED color (0-255)</param>
    /// <param name="b">Blue value of the LED color (0-255)</param>
    public void SetLED(byte r, byte g, byte b)
    {
        if (disconnected) return;

        psmove_set_leds(handle, (char)r, (char)g, (char)b);
    }

    /// <summary>
    /// Value of the analog trigger button (between 0 and 1)
    /// </summary
    public float Trigger
    {
        get { return trigger; }
    }

    /// <summary>
    /// The 3-axis acceleration values. 
    /// </summary>
    public Vector3 RawAcceleration
    {
        get { return rawAccel; }
    }

    /// <summary>
    /// The 3-axis acceleration values, roughly scaled between -3g to 3g (where 1g is Earth's gravity).
    /// </summary>
    public Vector3 Acceleration
    {
        get { return accel; }
    }

    /// <summary>
    /// The raw values of the 3-axis gyroscope. 
    /// </summary>
    public Vector3 RawGyroscope
    {
        get { return rawGyro; }
    }
    /// <summary>
    /// The raw values of the 3-axis gyroscope. 
    /// </summary>
    public Vector3 Gyro
    {
        get { return gyro; }
    }
    /// <summary>
    /// The  values of the quaternion orientation.
    /// </summary>
    public Quaternion Orientation
    {
        get { return orientation; }
    }
    /// <summary>
    /// The raw values of the 3-axis gyroscope. 
    /// </summary>
    public Vector3 Position
    {
        get { return position; }
    }

    /// <summary>
    /// The raw values of the 3-axis magnetometer.
    /// To be honest, we don't fully understand what the magnetometer does.
    /// The C API on which this code is based warns that this isn't fully tested.
    /// </summary>
    public Vector3 Magnetometer
    {
        get { return magnet; }
    }

    /// <summary>
    /// The battery level
    /// </summary>
    public PSMove_Battery_Level Battery
    {
        get { return battery; }
    }

    /// <summary>
    /// The temperature in Celcius
    /// </summary>
    public float Temperature
    {
        get { return temperature; }
    }

    /* TODO: These two values still need to be implemented, so we don't expose them publicly... yet!

	public float Battery 
	{
		get { return this.battery; }
	}
	
	public float Temperature 
	{
		get { return this.temperature; }
	}
	*/

    public PSMoveConnectionType ConnectionType
    {
        get { return psmove_connection_type(handle); }
    }

    #region private methods                              

    /// <summary>
    /// Process all the raw data on the Playstation Move controller
    /// </summary>
    private void ProcessData()
    {
        trigger = ((int)psmove_get_trigger(handle)) / 255f;

        /* <F> Comentados cálculos innecesarios para quitarle carga al juego y que funcione más fluido
        
        int x = 0, y = 0, z = 0;
        psmove_get_accelerometer(handle, ref x, ref y, ref z);

        rawAccel.x = x;
        rawAccel.y = y;
        rawAccel.z = z;


        float ax = 0, ay = 0, az = 0;
        psmove_get_accelerometer_frame(handle, PSMove_Frame.Frame_SecondHalf, ref ax, ref ay, ref az);

        accel.x = ax;
        accel.y = ay;
        accel.z = az;

        psmove_get_gyroscope(handle, ref x, ref y, ref z);

        rawGyro.x = x;
        rawGyro.y = y;
        rawGyro.z = z;


        float gx = 0, gy = 0, gz = 0;
        psmove_get_gyroscope_frame(handle, PSMove_Frame.Frame_SecondHalf, ref gx, ref gy, ref gz);

        gyro.x = gx;
        gyro.y = gy;
        gyro.z = gz;

        psmove_get_magnetometer(handle, ref x, ref y, ref z);

        // TODO: Should these values be converted into a more human-understandable range?
        magnet.x = x;
        magnet.y = y;
        magnet.z = z;

        battery = psmove_get_battery(handle);

        temperature = psmove_get_temperature(handle);

        */

        float px = 0, py = 0, pz = 0;
        if (cameraPosition != null)
        {
            if (cameraPosition == "Front")
            {
                psmove_fusion_get_position(fusion, handle, ref px, ref py, ref pz);

                position.x = px * 5;
                position.y = -py * 5;
                position.z = (-pz * 5);

                float rw = 0, rx = 0, ry = 0, rz = 0;
                psmove_get_orientation(handle, ref rw, ref rx, ref ry, ref rz);

                orientation.w = rw;
                orientation.x = rx;
                orientation.y = ry;
                orientation.z = rz;
            }
            else if (cameraPosition == "Back")
            {
                //<F> Estos valores se utilizan si se quiere ver el mando desde atrás
                psmove_fusion_get_position(fusion, handle, ref px, ref py, ref pz);

                position.x = -px * 5;
                position.y = -py * 5;
                position.z = -pz * 5;

                float rw = 0, rx = 0, ry = 0, rz = 0;
                psmove_get_orientation(handle, ref rw, ref rx, ref ry, ref rz);

                orientation.w = rw;
                orientation.x = rx;
                orientation.y = -ry;
                orientation.z = -rz;
            }
        }
    }

    #endregion


    #region importfunctions

    /* The following functions are bindings to Thomas Perl's C API for the PlayStation Move (http://thp.io/2010/psmove/)
	 * See README for more details.
	 * 
	 * NOTE! We have included bindings for the psmove_pair() function, even though we don't use it here
	 * See README and Pairing Utility code for more about pairing.
	 * 
	 * TODO: Expose hooks to psmove_get_btaddr() and psmove_set_btadd()
	 * These functions are already called by psmove_pair(), so unless you need to do something special, you won't need them.
	 */

    [DllImport("psmoveapi")]
    private static extern int psmove_count_connected();

    [DllImport("psmoveapi")]
    private static extern IntPtr psmove_connect();

    [DllImport("psmoveapi")]
    private static extern IntPtr psmove_connect_by_id(int id);

    [DllImport("psmoveapi")]
    private static extern int psmove_pair(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern PSMoveConnectionType psmove_connection_type(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern int psmove_has_calibration(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern void psmove_set_leds(IntPtr move, char r, char g, char b);

    [DllImport("psmoveapi")]
    private static extern int psmove_update_leds(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern void psmove_set_rumble(IntPtr move, char rumble);

    [DllImport("psmoveapi")]
    private static extern uint psmove_poll(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern uint psmove_get_buttons(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern uint psmove_get_button_events(IntPtr move, ref uint pressed, ref uint released);

    [DllImport("psmoveapi")]
    private static extern char psmove_get_trigger(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern float psmove_get_temperature(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern PSMove_Battery_Level psmove_get_battery(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern void psmove_get_accelerometer(IntPtr move, ref int ax, ref int ay, ref int az);

    [DllImport("psmoveapi")]
    private static extern void psmove_get_accelerometer_frame(IntPtr move, PSMove_Frame frame, ref float ax, ref float ay, ref float az);

    [DllImport("psmoveapi")]
    private static extern void psmove_get_gyroscope(IntPtr move, ref int gx, ref int gy, ref int gz);

    [DllImport("psmoveapi")]
    private static extern void psmove_get_gyroscope_frame(IntPtr move, PSMove_Frame frame, ref float gx, ref float gy, ref float gz);

    [DllImport("psmoveapi")]
    private static extern void psmove_get_magnetometer(IntPtr move, ref int mx, ref int my, ref int mz);

    [DllImport("psmoveapi")]
    private static extern void psmove_disconnect(IntPtr move);

    /*
	 * Orientation
	 */

    [DllImport("psmoveapi")]
    private static extern void psmove_enable_orientation(IntPtr move, int enabled);

    [DllImport("psmoveapi")]
    private static extern int psmove_has_orientation(IntPtr move);

    [DllImport("psmoveapi")]
    private static extern void psmove_get_orientation(IntPtr move, ref float w, ref float x, ref float y, ref float z);

    [DllImport("psmoveapi")]
    private static extern void psmove_reset_orientation(IntPtr move);

    /*
	 * Fusion
	 */
    [DllImport("psmoveapi_tracker")]
    private static extern IntPtr psmove_fusion_new(IntPtr tracker, float z_near, float z_far);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_fusion_free(IntPtr fusion);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_fusion_get_position(IntPtr fusion, IntPtr move, ref float x, ref float y, ref float z);

    /*
	 * Tracker
	 */
    [DllImport("psmoveapi_tracker")]
    private static extern IntPtr psmove_tracker_new();

    [DllImport("psmoveapi_tracker")]
    private static extern IntPtr psmove_tracker_new_with_camera(int camera);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_set_auto_update_leds(IntPtr tracker, IntPtr move,
        PSMove_Bool auto_update_leds);

    [DllImport("psmoveapi_tracker")]
    private static extern PSMove_Bool psmove_tracker_get_auto_update_leds(IntPtr tracker, IntPtr move);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_set_dimming(IntPtr tracker, float dimming);

    [DllImport("psmoveapi_tracker")]
    private static extern float psmove_tracker_get_dimming(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_set_exposure(IntPtr tracker, PSMoveTracker_Exposure exposure);

    [DllImport("psmoveapi_tracker")]
    private static extern PSMoveTracker_Exposure psmove_tracker_get_exposure(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_enable_deinterlace(IntPtr tracker, PSMove_Bool enabled);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_set_mirror(IntPtr tracker, int enabled);

    [DllImport("psmoveapi_tracker")]
    private static extern PSMove_Bool psmove_tracker_get_mirror(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern PSMoveTrackerStatus psmove_tracker_enable(IntPtr tracker, IntPtr move);

    [DllImport("psmoveapi_tracker")]
    private static extern PSMoveTracker_Status
    psmove_tracker_enable_with_color(IntPtr tracker, IntPtr move, byte r, byte g, byte b);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_disable(IntPtr tracker, IntPtr move);

    [DllImport("psmoveapi_tracker")]
    private static extern int psmove_tracker_get_color(IntPtr tracker, IntPtr move,
        ref byte r, ref byte g, ref byte b);

    [DllImport("psmoveapi_tracker")]
    private static extern int psmove_tracker_get_camera_color(IntPtr tracker, IntPtr move,
    ref byte r, ref byte g, ref byte b);

    [DllImport("psmoveapi_tracker")]
    private static extern int psmove_tracker_set_camera_color(IntPtr tracker, IntPtr move,
        byte r, byte g, byte b);

    [DllImport("psmoveapi_tracker")]
    private static extern PSMoveTracker_Status psmove_tracker_get_status(IntPtr tracker, IntPtr move);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_free(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_update_image(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern int psmove_tracker_update(IntPtr tracker, IntPtr move);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_annotate(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern IntPtr psmove_tracker_get_frame(IntPtr tracker);

    [DllImport("psmoveapi_tracker")]
    private static extern int psmove_tracker_get_position(IntPtr tracker,
        IntPtr move, ref float x, ref float y, ref float radius);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_get_size(IntPtr tracker,
    ref int width, ref int height);

    [DllImport("psmoveapi_tracker")]
    private static extern float psmove_tracker_distance_from_radius(IntPtr tracker,
        float radius);

    [DllImport("psmoveapi_tracker")]
    private static extern void psmove_tracker_set_distance_parameters(IntPtr tracker,
    float height, float center, float hwhm, float shape);

    /*
	 * psmove_fusion_get_projection_matrix(fusion)
	 * psmove_fusion_get_modelview_matrix(fusion, move)
	 */

    #endregion
}
