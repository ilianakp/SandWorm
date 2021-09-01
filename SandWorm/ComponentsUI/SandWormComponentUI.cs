﻿using System;

namespace SandWorm
{
    class SandWormComponentUI
    {
        #region Class variables

        public static bool _reset = false;
        public static bool _resize = false;

        public static MenuDropDown _sensorType;
        public static MenuDropDown _refreshRate;
        public static MenuSlider _sensorElevation;
        public static MenuCheckBox _calibrate;
        public static MenuSlider _leftColumns;
        public static MenuSlider _rightColumns;
        public static MenuSlider _topRows;
        public static MenuSlider _bottomRows;
        public static MenuSlider _cycleInterval;

        public static MenuDropDown _outputType;
        public static MenuDropDown _analysisType;
        public static MenuDropDown _colorPalette;
        public static MenuSlider _colorGradientRange;
        public static MenuSlider _contourIntervalRange;
        public static MenuSlider _waterLevel;
        public static MenuSlider _contourRoughness;

        public static MenuSlider _averagedFrames;
        public static MenuSlider _blurRadius;

        #endregion
        public static void MainComponentUI(GH_ExtendableComponentAttributes attr)
        {
            #region Sensor Type
            MenuPanel optionsMenuPanel = new MenuPanel(0, "panel_options")
            {
                Name = "Options for the SandWorm component.", // <- mouse over header for the entire "options" fold-out.
                Header = "Define custom parameters here." // <- mouse over description
            };

            GH_ExtendableMenu optionsMenu = new GH_ExtendableMenu(0, "menu_options")
            {
                Name = "Sensor", // <- Foldable header
                Header = "Setup the Kinect Sensor." // <- Foldable mouseOver
            };

            MenuStaticText sensorTypeHeader = new MenuStaticText("Sensor type", "Choose which Kinect Version you have.");
            _sensorType = new MenuDropDown(0, "Sensor type", "Choose Kinect Version");
            _sensorType.AddItem("Kinect Azure Narrow", "Kinect Azure Narrow");
            _sensorType.AddItem("Kinect Azure Wide", "Kinect Azure Wide");
            _sensorType.AddItem("Kinect for Windows", "Kinect for Windows");

            _sensorType.ValueChanged += _sensorType__ValueChanged;

            MenuStaticText refreshRateHeader = new MenuStaticText("Refresh rate", "Choose the refresh rate of the model.");
            _refreshRate = new MenuDropDown(11111, "Refresh rate", "Choose Refresh Rate");
            _refreshRate.AddItem("Max", "Max");
            _refreshRate.AddItem("15 FPS", "15 FPS");
            _refreshRate.AddItem("5 FPS", "5 FPS");
            _refreshRate.AddItem("1 FPS", "1 FPS");
            _refreshRate.AddItem("0.2 FPS", "0.2 FPS");

            MenuStaticText sensorElevationHeader = new MenuStaticText("Sensor elevation", "Distance between the sensor and the table. \nInput should be in millimeters.\nTo automatically estimate this value, check the 'Calibrate' checkbox and reset.");
            _sensorElevation = new MenuSlider(sensorElevationHeader, 1, 0, 1500, 1000, 0);
            _calibrate = new MenuCheckBox(10001, "Calibrate", "Calibrate");

            MenuStaticText leftColumnsHeader = new MenuStaticText("Left columns", "Number of pixels to trim from the left.");
            _leftColumns = new MenuSlider(leftColumnsHeader, 2, 0, 200, 50, 0);
            _leftColumns.ValueChanged += _slider__ValueChanged;

            MenuStaticText rightColumnsHeader = new MenuStaticText("Right columns", "Number of pixels to trim from the right.");
            _rightColumns = new MenuSlider(rightColumnsHeader, 3, 0, 200, 50, 0);
            _rightColumns.ValueChanged += _slider__ValueChanged;

            MenuStaticText topRowsHeader = new MenuStaticText("Top rows", "Number of pixels to trim from the top.");
            _topRows = new MenuSlider(topRowsHeader, 4, 0, 200, 50, 0);
            _topRows.ValueChanged += _slider__ValueChanged;

            MenuStaticText bottomRowsHeader = new MenuStaticText("Bottom rows", "Number of pixels to trim from the bottom.");
            _bottomRows = new MenuSlider(bottomRowsHeader, 5, 0, 200, 50, 0);
            _bottomRows.ValueChanged += _slider__ValueChanged;

            optionsMenu.AddControl(optionsMenuPanel);
            attr.AddMenu(optionsMenu);

            optionsMenuPanel.AddControl(sensorTypeHeader);
            optionsMenuPanel.AddControl(_sensorType);
            optionsMenuPanel.AddControl(refreshRateHeader);
            optionsMenuPanel.AddControl(_refreshRate);
            optionsMenuPanel.AddControl(sensorElevationHeader);
            optionsMenuPanel.AddControl(_sensorElevation);
            optionsMenuPanel.AddControl(_calibrate);
            optionsMenuPanel.AddControl(leftColumnsHeader);
            optionsMenuPanel.AddControl(_leftColumns);
            optionsMenuPanel.AddControl(rightColumnsHeader);
            optionsMenuPanel.AddControl(_rightColumns);
            optionsMenuPanel.AddControl(topRowsHeader);
            optionsMenuPanel.AddControl(_topRows);
            optionsMenuPanel.AddControl(bottomRowsHeader);
            optionsMenuPanel.AddControl(_bottomRows);

            #endregion

            #region Analysis
            MenuPanel analysisPanel = new MenuPanel(20, "panel_analysis")
            {
                Name = "Analysis",
                Header = "Define custom analysis parameters."
            };
            GH_ExtendableMenu analysisMenu = new GH_ExtendableMenu(21, "menu_analysis")
            {
                Name = "Analysis",
                Header = "Define custom analysis parameters."
            };

            MenuStaticText outputTypeHeader = new MenuStaticText("Output type", "Choose which type of geometry to output.");
            _outputType = new MenuDropDown(22, "Ouput type", "Choose type of geometry to output.");
            _outputType.AddItem("Mesh", "Mesh");
            _outputType.AddItem("Point Cloud", "Point Cloud");

            MenuStaticText analysisTypeHeader = new MenuStaticText("Analysis type", "Choose which type of analysis to perform on scanned geometry.");
            _analysisType = new MenuDropDown(23, "Ouput type", "Choose type of geometry to output.");
            _analysisType.AddEnum(typeof(Structs.AnalysisTypes));
            _analysisType.Value = (int)Structs.AnalysisTypes.Elevation;

            MenuStaticText colorPaletteHeader = new MenuStaticText("Color palette", "Choose color palette for elevation analysis.");
            _colorPalette = new MenuDropDown(231, "Color palette", "Choose color palette for elevation analysis.");
            _colorPalette.AddEnum(typeof(Structs.ColorPalettes));
            _colorPalette.Value = (int)Structs.ColorPalettes.Europe;
            
            MenuStaticText colorGradientHeader = new MenuStaticText("Color gradient range", "Define maximum elevation for color gradient. \nInput should be in millimeters.");
            _colorGradientRange = new MenuSlider(colorGradientHeader, 24, 15, 100, 40, 0);

            MenuStaticText contourIntervalHeader = new MenuStaticText("Contour interval", "Define spacing between contours. \nInput should be in millimeters.");
            _contourIntervalRange = new MenuSlider(contourIntervalHeader, 25, 0, 30, 0, 0);

            MenuStaticText waterLevelHeader = new MenuStaticText("Water level", "Define distance between the table and a simulated water surface. \nInput should be in millimeters.");
            _waterLevel = new MenuSlider(waterLevelHeader, 26, 0, 300, 0, 0);

            MenuStaticText contourRoughnessHeader = new MenuStaticText("Contour roughness", "Specify how rough contour sampling should be.");
            _contourRoughness = new MenuSlider(contourRoughnessHeader, 27, 1, 20, 2, 0);

            MenuStaticText analysisCycle = new MenuStaticText("Analysis cycle interval", "Define time in minutes to cycle through all the analysis types. 0 = Manually control the analysis type.");
            _cycleInterval = new MenuSlider(analysisCycle, 28, 0, 60, 0, 0);


            analysisMenu.AddControl(analysisPanel);
            attr.AddMenu(analysisMenu);

            analysisPanel.AddControl(outputTypeHeader);
            analysisPanel.AddControl(_outputType);
            analysisPanel.AddControl(analysisTypeHeader);
            analysisPanel.AddControl(_analysisType);
            analysisPanel.AddControl(colorPaletteHeader);
            analysisPanel.AddControl(_colorPalette);
            analysisPanel.AddControl(colorGradientHeader);
            analysisPanel.AddControl(_colorGradientRange);
            analysisPanel.AddControl(contourIntervalHeader);
            analysisPanel.AddControl(_contourIntervalRange);
            analysisPanel.AddControl(contourRoughnessHeader);
            analysisPanel.AddControl(_contourRoughness);
            analysisPanel.AddControl(waterLevelHeader);
            analysisPanel.AddControl(_waterLevel);
            analysisPanel.AddControl(analysisCycle);
            analysisPanel.AddControl(_cycleInterval);

            #endregion

            #region Post processing

            MenuPanel postProcessingPanel = new MenuPanel(40, "panel_analysis")
            {
                Name = "Post Processing",
                Header = "Define custom post processing parameters."
            };
            GH_ExtendableMenu postProcessingMenu = new GH_ExtendableMenu(41, "menu_analysis")
            {
                Name = "Post Processing",
                Header = "Define custom post processing parameters."
            };

            MenuStaticText averagedFramesHeader = new MenuStaticText("Averaged frames", "Number of frames to average across.");
            _averagedFrames = new MenuSlider(averagedFramesHeader, 42, 1, 30, 5, 0);
            _averagedFrames.ValueChanged += _slider__ValueChanged;

            MenuStaticText blurRadiusHeader = new MenuStaticText("Blur Radius", "Define the extent of gaussian blurring.");
            _blurRadius = new MenuSlider(blurRadiusHeader, 43, 0, 15, 4, 0);

            postProcessingMenu.AddControl(postProcessingPanel);
            attr.AddMenu(postProcessingMenu);

            postProcessingPanel.AddControl(averagedFramesHeader);
            postProcessingPanel.AddControl(_averagedFrames);
            postProcessingPanel.AddControl(blurRadiusHeader);
            postProcessingPanel.AddControl(_blurRadius);

            #endregion

            #region Callbacks
            void _sensorType__ValueChanged(object sender, EventArgs e)
            {
                _reset = true;
            }

            void _slider__ValueChanged(object sender, EventArgs e)
            {
                _resize = true;
            }

            #endregion
        }

    }
}
