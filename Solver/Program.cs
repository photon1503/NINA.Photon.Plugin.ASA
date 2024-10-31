

using nom.tam.fits;

string folder = @"X:\seqsample\nosync\2024-10-22-06-40";
string poxFileName = @"X:\seqsample\nosync\2024-10-22-06-40\nina-pox.pox";

StreamWriter writer = new StreamWriter(poxFileName, true);

// read all files in the folder
string[] files = Directory.GetFiles(folder, "*.fits", SearchOption.AllDirectories);

foreach (string file in files)
{
    if (File.Exists(file))
    {
        Console.WriteLine($"Processing {file}");

        //read fits header
        nom.tam.fits.Fits fits = new nom.tam.fits.Fits(file);
        BasicHDU hdu = fits.ReadHDU();
        if (hdu != null)
        {
            // Get the header from the HDU
            Header header = hdu.Header;

            // Retrieve specific header values using their keywords
            string objectName = header.GetStringValue("OBJECT");
            string objctra = header.GetStringValue("OBJCTRA");
            string objctdec = header.GetStringValue("OBJCTDEC");
            string dateobs = header.GetStringValue("DATE-OBS");
            string pierSide = header.GetStringValue("NOTES");


            Console.WriteLine($"OBJCTRA: {objctra}");
            Console.WriteLine($"OBJCTDEC: {objctdec}");
            Console.WriteLine($"DATE-OBS: {dateobs}");
        }

    }
}