using System;

[Serializable]

public class Planet
{
    public string body;
    public DateTime date;

    public double raDeg;
    public double decDeg;

    public double xArcsec;
    public double yArcsec;

    public double xRad;
    public double yRad;

    public double distanceAU;
    public double magnitude;

    public override string ToString()
    {
        return $"{body}° | RA {raDeg}° Dec {decDeg}° | Dist [{distanceAU}] AU";
    }
}
