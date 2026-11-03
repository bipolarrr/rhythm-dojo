namespace RhythmDojo.Application
{
    public static class InputTiming
    {
        public static double Calibrate(double songTime, double timingOffsetSeconds)
        {
            return songTime - timingOffsetSeconds;
        }
    }
}
