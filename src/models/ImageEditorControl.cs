using System.Windows;

namespace ScreenLookup.src.models
{
    internal class ImageEditorControl
    {
        public bool IsEditing = false;

        // Target represents the desired final state.
        public ControlUnit RotationTarget { get; set; } = new() { Value = 0.0, DraggingValue = 0.0 };
        public ControlUnit ScaleTarget { get; set; } = new() { Value = 1.0, DraggingValue = 0.0 };

        // Current represents the state of the actual bitmap in memory.
        public ControlUnit RotationCurrent { get; set; } = new() { Value = 0.0 };
        public ControlUnit ScaleCurrent { get; set; } = new() { Value = 1.0 };

        internal class ControlUnit
        {
            public double Value = 0.0;
            public bool IsDragging = false;
            public double DraggingValue = 0.0; // Fixed typo: DragingValue -> DraggingValue
            public double StartValue = 0.0;
            public Point StartDraggingPoint;
            public double DeltaValue = 0.0; // Used for non-additive transformations
        }

        public void StopDragging()
        {
            RotationTarget.DraggingValue = 0.0;
            ScaleTarget.DraggingValue = 0.0;
        }

        public void SubmitCurrent()
        {
            IsEditing = false;

            RotationCurrent.Value = RotationTarget.Value;
            ScaleCurrent.Value = ScaleTarget.Value;

            RotationTarget.DraggingValue = 0.0;
            ScaleTarget.DraggingValue = 0.0;
        }

        public void Reset()
        {
            IsEditing = false;

            RotationTarget.Value = 0.0;
            RotationTarget.DraggingValue = 0.0;
            RotationCurrent.Value = 0.0;

            ScaleTarget.Value = 1.0;
            ScaleTarget.DraggingValue = 0.0;
            ScaleCurrent.Value = 1.0;
        }

        public void ChangeRotation(double value)
        {
            value = Math.Round(value, 1);
            IsEditing = true;

            RotationTarget.Value = value;
            RotationTarget.DraggingValue = RotationTarget.Value - RotationCurrent.Value;
        }


        public void ChangeScale(double value)
        {
            value = Math.Min(5.0, Math.Max(0.2, Math.Round(value, 2)));
            IsEditing = true;

            ScaleTarget.Value = value;
            ScaleTarget.DraggingValue = ScaleCurrent.Value > 0 ? (value / ScaleCurrent.Value) : 1.0;
        }
    }
}
