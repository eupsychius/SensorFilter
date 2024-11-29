using System;

public class SensorCoefficients
{
    public int      Id                  { get; set; }
    public string   SerialNumber        { get; set; }
    public int      CoefficientIndex    { get; set; }
    public double   CoefficientValue    { get; set; }
    public string   Model               { get; set; }
    public string   Type                { get; set; }
    public DateTime CoefficientsDate    { get; set; }
}
