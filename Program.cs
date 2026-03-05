namespace C2_BMD_to_sprites
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string logical = "ls_gui_window.bmd";
            string inputBmdPath = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\lang\ger\bobs\ls_gui_window_original.bmd";
            string palettesDir = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\palettes";
            string outputDir = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\lang\ger\bobs\ls_gui_window";

            BmdConverter.ConvertFileToPngFramesUsingGuiRules(inputBmdPath, outputDir, palettesDir, logical);

            string originalBmdPath = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\lang\ger\bobs\ls_gui_window_original.bmd";
            string inputFramesDirectory = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\lang\ger\bobs\ls_gui_window";
            string outputBmdPath = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\lang\ger\bobs\ls_gui_window.bmd";
            string palettesDirectory = @"E:\Steam\steamapps\common\Cultures 8th Wonder\data\gui\palettes";

            BmdConverter.EncodeBmdFromPngFramesUsingGuiRules(originalBmdPath, inputFramesDirectory, outputBmdPath, palettesDirectory, logical);

            return 0;
        }
    }
}