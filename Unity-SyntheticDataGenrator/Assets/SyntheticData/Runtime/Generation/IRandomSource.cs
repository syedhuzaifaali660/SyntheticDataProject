namespace SyntheticData.Generation
{
    public interface IRandomSource
    {
        int Range(int minimum, int maximumExclusive);
        float Range(float minimum, float maximum);
    }
}
