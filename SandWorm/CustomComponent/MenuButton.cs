using System;
using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;

namespace SandWorm
{
    public class MenuButton : GH_Attr_Widget
    {
        private bool _active;
		private int _buttonHeight = 20;
		private Rectangle _buttonBounds;
		private int _verticalPadding = 8;
		private string _toolTipText;

		public bool Active
		{
			get 
			{
				return _active;
			}
			set
			{
				_active = value;
			}
		}

		public override string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public MenuButton(int index, string id, string toolTip) : base(index, id)
		{
			Name = id;
			_toolTipText = toolTip;
		}

		public override SizeF ComputeMinSize()
		{
			float height = TextRenderer.MeasureText("1", WidgetServer.Instance.SliderValueTagFont).Height;
			return new System.Drawing.SizeF(20f, _buttonHeight + _verticalPadding);
		}

		public override void PostUpdateBounds(out float outHeight)
		{
			outHeight = ComputeMinSize().Height;
		}

		public override void Layout()
		{
			_buttonBounds = new Rectangle(
				(int)base.CanvasPivot.X, (int)base.CanvasPivot.Y + (int)(_verticalPadding / 2), 
				(int)base.Width, _buttonHeight + _verticalPadding);
		}

		public override void Render(WidgetRenderArgs args)
		{
			Graphics graphics = args.Canvas.Graphics;
			GH_Capsule button;
			var buttonBox = _buttonBounds;
			buttonBox.Height -= _verticalPadding;

			if (_active)
				button = GH_Capsule.CreateTextCapsule(buttonBox, buttonBox, GH_Palette.Grey, "Calibrating...", 1, 0);
			else
				button = GH_Capsule.CreateTextCapsule(buttonBox, buttonBox, GH_Palette.Black, this.Name, 1, 0);

			button.Render(graphics, _active, false, false);
			button.Dispose();
		}

		public override void TooltipSetup(System.Drawing.PointF canvasPoint, GH_TooltipDisplayEventArgs e)
		{
			e.Icon = null;
			e.Title = _name + " (Button)";
			e.Text = _toolTipText;
		}

		public override GH_Attr_Widget IsTtipPoint(PointF pt)
		{
			if (_buttonBounds.Contains((int)pt.X, (int)pt.Y))
			{
				return this;
			}
			return null;
		}

		public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
		{
			return GH_ObjectResponse.Capture;
		}

		public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
		{
			if (base.CanvasBounds.Contains(e.CanvasLocation))
			{
				_active = true;
			}
			return GH_ObjectResponse.Release;
		}

		public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
		{
			// Checking if it's a left click, and if it's in the button's area
			if (e.Button == System.Windows.Forms.MouseButtons.Left && ((RectangleF)_buttonBounds).Contains(e.CanvasLocation))
			{
				return GH_ObjectResponse.Handled;
			}
			return GH_ObjectResponse.Ignore;
		}
	}
}