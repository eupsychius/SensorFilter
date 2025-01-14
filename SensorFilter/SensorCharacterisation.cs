using System;

public class SensorCharacterisation
{
    public int      CharacterisationId  { get; set; }
    public int      SensorId            { get; set; }
    public DateTime DateTime            { get; set; }
    public double   Temperature         { get; set; }
    public int      Range               { get; set; }
    public double   Pressure            { get; set; }
    public double   Voltage             { get; set; }
    public double   Resistance          { get; set; }
    public double   Deviation           { get; set; }
}