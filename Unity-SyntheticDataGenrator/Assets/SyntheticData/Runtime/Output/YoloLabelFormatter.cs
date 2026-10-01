using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SyntheticData.Labels;

namespace SyntheticData.Output
{
    public static class YoloLabelFormatter
    {
        public static string Format(IReadOnlyList<YoloBox> boxes)
        {
            var output = new StringBuilder();
            for (var index = 0; index < boxes.Count; index++)
            {
                var box = boxes[index];
                output.Append(box.ClassId);
                output.Append(' ');
                output.Append(box.CenterX.ToString("F6", CultureInfo.InvariantCulture));
                output.Append(' ');
                output.Append(box.CenterY.ToString("F6", CultureInfo.InvariantCulture));
                output.Append(' ');
                output.Append(box.Width.ToString("F6", CultureInfo.InvariantCulture));
                output.Append(' ');
                output.Append(box.Height.ToString("F6", CultureInfo.InvariantCulture));
                output.Append('\n');
            }

            return output.ToString();
        }
    }
}
