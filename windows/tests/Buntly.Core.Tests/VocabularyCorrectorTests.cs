namespace Buntly.Core.Tests;

public class VocabularyCorrectorTests
{
    [Fact]
    public void Fixes_the_measured_resyndication_mangling()
    {
        Assert.Equal(
            "before the resyndication closed escrow",
            VocabularyCorrector.Apply("before the resindication closed escrow"));
    }

    [Fact]
    public void Fixes_the_measured_lightech_mangling()
    {
        Assert.Equal(
            "the LIHTC deal closes in march",
            VocabularyCorrector.Apply("the lightech deal closes in march"));
    }

    [Fact]
    public void Normalizes_kbtu_casing()
    {
        Assert.Equal(
            "42 kBtu per square foot",
            VocabularyCorrector.Apply("42 kbtu per square foot"));
    }

    [Fact]
    public void Ordinary_text_is_unchanged()
    {
        const string ordinary = "The quick brown fox jumps over the lazy dog. Remember to call the dentist tomorrow.";
        Assert.Equal(ordinary, VocabularyCorrector.Apply(ordinary));
    }

    [Fact]
    public void Does_not_corrupt_common_words()
    {
        Assert.Equal("What's the cap on this?", VocabularyCorrector.Apply("What's the cap on this?"));
        Assert.Equal("Amy said she's rad.", VocabularyCorrector.Apply("Amy said she's rad."));
        Assert.Equal("Am I free tomorrow?", VocabularyCorrector.Apply("Am I free tomorrow?"));
        Assert.Equal("the capital city", VocabularyCorrector.Apply("the capital city"));
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        Assert.Equal(
            "signed a HAP contract yesterday",
            VocabularyCorrector.Apply("signed a HAPP CONTRACT yesterday"));
    }

    [Fact]
    public void User_corrections_apply_after_the_built_in_list()
    {
        var rows = new[] { new UserCorrection("loo tech", "LIHTC") };
        Assert.Equal(
            "the LIHTC allocation and the resyndication both closed",
            VocabularyCorrector.Apply("the loo tech allocation and the resindication both closed", rows));
    }

    [Fact]
    public void A_dollar_sign_in_a_user_correction_stays_a_dollar_sign()
    {
        var rows = new[] { new UserCorrection("dee two", "D2 $4") };
        Assert.Equal("use D2 $4 here", VocabularyCorrector.Apply("use dee two here", rows));
    }
}
