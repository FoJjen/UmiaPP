namespace UmiaPP.Services;

internal static class PurchaseImportFormatHelp
{
    public const string Title = "Formatguide för import";

    public const string Text = """
        ÄMNE I XML – EXEMPEL
        100-90 Böj → Kanal-detaljer, Böj 90°, dimension Ø100.
        100-45 Böj → samma diameter men 45° vinkel; summeras separat.
        200 Kanal → Kanaler, dimension Ø200, mängd i meter.
        315-200 Sadel Avstick → Sadelavstick, dimension 315-200.
        400-200 T-rör → T-rör, dimension 400-200.
        400-200-160 T-rör → alla tre anslutningsmåtten behålls i ordning.
        160-100 Dim GG → Dim GG, dimension 160-100.
        250-200 Dim Exentrik → Dim Exentrik, dimension 250-200.

        För en böj är första talet diametern och andra vinkeln.
        För avstick, T-rör och Dim är talen anslutningsdiametrar.
        GG betyder Nippel/Nippel. Behåll GG och Exentrik i artikeltexten.

        VID GRANSKNING
        Välj kategori och ange Typ / Artikel samt Dimension separat.
        Exempel: Kanal-detaljer / Böj 90° / 100, eller Kanal-detaljer / Dim GG / 160-100.
        Ändrad kategori, artikeltext eller dimension gäller alla rader med exakt samma ursprungliga Ämne, även på andra ritningar.
        Antal, längd och vertikalhöjd ändras bara på den aktuella raden. Tomma ämnen kopplas inte ihop.
        Till förhandsgranskning utan fler ändringar behåller tidigare uppgifter.
        I förhandsgranskningen går Tillbaka, Esc och stängningskrysset till redigering.
        Importera sparar listorna. Avbryt import avslutar utan att spara.

        Manuellt ändrade kategorival sparas lokalt efter bekräftad import och återanvänds för exakt samma ursprungliga Ämne i alla projekt, även efter omstart.
        Ett nytt kategorival i granskningen ersätter det tidigare valet när importen bekräftas. Mängder och dimensioner sparas inte som kategoriregler.

        Kanalernas meter behålls. Inköpsantal beräknas som summerad längd / 3, avrundat uppåt.
        """;
}
