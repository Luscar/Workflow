using SimpleBPM;

namespace SimpleBPM.Tests;

public class FiltreVariableTests
{
    [Fact]
    public void Constructor_ParDefaut()
    {
        var filtre = new FiltreVariable();

        Assert.Equal(string.Empty, filtre.NomVariable);
        Assert.Null(filtre.Valeur);
        Assert.Equal(OperateurFiltre.Egal, filtre.Operateur);
        Assert.Equal(TypeDonnee.Texte, filtre.TypeDonnee);
    }

    [Fact]
    public void Constructor_AvecParametres()
    {
        var filtre = new FiltreVariable("nom", "test", OperateurFiltre.Contient, TypeDonnee.Texte);

        Assert.Equal("nom", filtre.NomVariable);
        Assert.Equal("test", filtre.Valeur);
        Assert.Equal(OperateurFiltre.Contient, filtre.Operateur);
        Assert.Equal(TypeDonnee.Texte, filtre.TypeDonnee);
    }

    [Fact]
    public void Correspond_Texte_Egal()
    {
        var filtre = new FiltreVariable("statut", "actif");

        Assert.True(filtre.Correspond("actif"));
    }

    [Fact]
    public void Correspond_Texte_Different()
    {
        var filtre = new FiltreVariable("statut", "actif", OperateurFiltre.Different);

        Assert.True(filtre.Correspond("inactif"));
    }

    [Fact]
    public void Correspond_Texte_Contient()
    {
        var filtre = new FiltreVariable("desc", "test", OperateurFiltre.Contient);

        Assert.True(filtre.Correspond("ceci est un test"));
    }

    [Fact]
    public void Correspond_Texte_CommencePar()
    {
        var filtre = new FiltreVariable("code", "PRE", OperateurFiltre.CommencePar);

        Assert.True(filtre.Correspond("PREFIX-001"));
        Assert.False(filtre.Correspond("NOPRE"));
    }

    [Fact]
    public void Correspond_Texte_FinitPar()
    {
        var filtre = new FiltreVariable("ext", ".pdf", OperateurFiltre.FinitPar);

        Assert.True(filtre.Correspond("document.pdf"));
        Assert.False(filtre.Correspond("document.doc"));
    }

    [Fact]
    public void Correspond_Nombre_Egal()
    {
        var filtre = new FiltreVariable("montant", 100.0, OperateurFiltre.Egal, TypeDonnee.Nombre);

        Assert.True(filtre.Correspond(100.0));
    }

    [Fact]
    public void Correspond_Nombre_Superieur()
    {
        var filtre = new FiltreVariable("montant", 100.0, OperateurFiltre.Superieur, TypeDonnee.Nombre);

        Assert.True(filtre.Correspond(200.0));
        Assert.False(filtre.Correspond(50.0));
    }

    [Fact]
    public void Correspond_Nombre_AvecTypesIntEtLong()
    {
        var filtre = new FiltreVariable("quantite", 5, OperateurFiltre.Egal, TypeDonnee.Nombre);

        Assert.True(filtre.Correspond(5));
        Assert.True(filtre.Correspond(5L));
    }

    [Fact]
    public void Correspond_Date_Egal()
    {
        var date = new DateTime(2026, 3, 15);
        var filtre = new FiltreVariable("echeance", date, OperateurFiltre.Egal, TypeDonnee.Date);

        Assert.True(filtre.Correspond(date));
    }

    [Fact]
    public void Correspond_Date_Superieur()
    {
        var filtre = new FiltreVariable("echeance", new DateTime(2026, 1, 1), OperateurFiltre.Superieur, TypeDonnee.Date);

        Assert.True(filtre.Correspond(new DateTime(2026, 6, 1)));
        Assert.False(filtre.Correspond(new DateTime(2025, 6, 1)));
    }

    [Fact]
    public void Correspond_Booleen_Egal()
    {
        var filtre = new FiltreVariable("actif", true, OperateurFiltre.Egal, TypeDonnee.Booleen);

        Assert.True(filtre.Correspond(true));
        Assert.False(filtre.Correspond(false));
    }

    [Fact]
    public void Correspond_Booleen_Different()
    {
        var filtre = new FiltreVariable("actif", true, OperateurFiltre.Different, TypeDonnee.Booleen);

        Assert.True(filtre.Correspond(false));
        Assert.False(filtre.Correspond(true));
    }

    [Fact]
    public void Correspond_NullsEgaux()
    {
        var filtre = new FiltreVariable("var", null, OperateurFiltre.Egal);

        Assert.True(filtre.Correspond(null));
    }

    [Fact]
    public void Correspond_UnSeulNull_Different()
    {
        var filtre = new FiltreVariable("var", "valeur", OperateurFiltre.Different);

        Assert.True(filtre.Correspond(null));
    }
}
