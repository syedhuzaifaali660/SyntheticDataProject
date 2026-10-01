namespace SyntheticData.Labels
{
    public readonly struct YoloBox
    {
        public int ClassId { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public float Width { get; }
        public float Height { get; }

        public YoloBox(int classId, float centerX, float centerY, float width, float height)
        {
            ClassId = classId;
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
        }
    }
}
