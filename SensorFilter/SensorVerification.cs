using System;

public class SensorVerification
{
    public  int         VerificationId  { get; set; }
    public  int         SensorId        { get; set; }
    public  DateTime    DateTime        { get; set; }
    public  double      Temperature     { get; set; }
    public  double      NPI             { get; set; }
    public  double      VPI             { get; set; }
    public  double      PressureGiven   { get; set; }
    public  double      PressureReal    { get; set; }
    public  double      CurrentGiven    { get; set; }
    public  double      CurrentReal     { get; set; }
    public  double      Voltage         { get; set; }
    public  double      Resistance      { get; set; }
}
