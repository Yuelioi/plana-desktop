# Use a WPF shell with the existing web renderer

Plana Desktop uses C# and WPF for Windows lifecycle, transparent hit testing, tray integration, and native actions while WebView2CompositionControl hosts the existing Spine renderer. The prototype proved transparent Spine rendering and native click-through work together; rewriting the renderer would add risk without improving the desktop integration that motivated the new application.
