using System;

public class SensorData
{
    public int      DataId          { get; set; }
    public string   SerialNumber    { get; set; }
    public string   Type            { get; set; }
    public string   Model           {  get; set; }
    public DateTime DateTime        { get; set; }
    public double   Temperature     { get; set; }
    public int      Range           { get; set; }
    public double   Pressure        { get; set; }
    public double   Voltage         { get; set; }
    public double   Resistance      { get; set; }
    public double   Deviation       { get; set; }
}
