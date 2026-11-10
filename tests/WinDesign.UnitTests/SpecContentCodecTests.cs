using WinDesign.Contracts.Equipment;

namespace WinDesign.UnitTests;

public class SpecContentCodecTests
{
    [Fact]
    public void Parse_SeedJson_ReturnsOrderedStringParameters()
    {
        var parameters = SpecContentCodec.Parse(
            "{\"range\":\"0~1.6MPa\",\"output\":\"4~20mA\",\"accuracy\":\"0.075%\"}",
            out var warning);

        Assert.Null(warning);
        Assert.Equal(new[] { "range", "output", "accuracy" }, parameters.Select(x => x.Key));
        Assert.Equal("0~1.6MPa", parameters[0].Value);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmptyAndWarning()
    {
        var parameters = SpecContentCodec.Parse("not json", out var warning);

        Assert.Empty(parameters);
        Assert.NotNull(warning);
    }

    [Fact]
    public void Parse_NonObjectRoot_ReturnsEmptyAndWarning()
    {
        var parameters = SpecContentCodec.Parse("[1,2,3]", out var warning);

        Assert.Empty(parameters);
        Assert.NotNull(warning);
    }

    [Fact]
    public void Parse_NonStringValues_ConvertsToRawText()
    {
        var parameters = SpecContentCodec.Parse(
            "{\"qty\":1,\"ok\":true,\"nested\":{\"a\":\"b\"},\"empty\":null}",
            out var warning);

        Assert.Equal("1", parameters.Single(x => x.Key == "qty").Value);
        Assert.Equal("true", parameters.Single(x => x.Key == "ok").Value);
        Assert.Equal("{\"a\":\"b\"}", parameters.Single(x => x.Key == "nested").Value);
        Assert.Equal(string.Empty, parameters.Single(x => x.Key == "empty").Value);
        Assert.NotNull(warning);
    }

    [Fact]
    public void Parse_DuplicateKeys_KeepsFirstAndWarns()
    {
        var parameters = SpecContentCodec.Parse(
            "{\"range\":\"first\",\"range\":\"second\"}",
            out var warning);

        Assert.Single(parameters);
        Assert.Equal("first", parameters[0].Value);
        Assert.NotNull(warning);
    }

    [Fact]
    public void TryNormalize_DropsBlankRowsAndTrims()
    {
        var ok = SpecContentCodec.TryNormalize(
            new[]
            {
                new SpecParameterDto { Key = "  range  ", Value = "  0~1.6MPa  " },
                new SpecParameterDto { Key = " ", Value = " " }
            },
            out var normalized,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        var parameter = Assert.Single(normalized);
        Assert.Equal("range", parameter.Key);
        Assert.Equal("0~1.6MPa", parameter.Value);
    }

    [Fact]
    public void TryNormalize_BlankKeyWithValue_Fails()
    {
        var ok = SpecContentCodec.TryNormalize(
            new[] { new SpecParameterDto { Key = " ", Value = "value" } },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("参数名", error);
    }

    [Fact]
    public void TryNormalize_DuplicateKeysIgnoringCase_Fails()
    {
        var ok = SpecContentCodec.TryNormalize(
            new[]
            {
                new SpecParameterDto { Key = "Range", Value = "a" },
                new SpecParameterDto { Key = "range", Value = "b" }
            },
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("重复", error);
    }

    [Fact]
    public void TryNormalize_TooManyParameters_Fails()
    {
        var rows = Enumerable.Range(0, SpecContentCodec.MaxParameters + 1)
            .Select(i => new SpecParameterDto { Key = "k" + i, Value = "v" });

        var ok = SpecContentCodec.TryNormalize(rows, out _, out var error);

        Assert.False(ok);
        Assert.Contains("参数数量", error);
    }

    [Fact]
    public void Serialize_RoundTripsThroughParse()
    {
        var input = new[]
        {
            new SpecParameterDto { Key = "量程", Value = "0~1.6MPa" },
            new SpecParameterDto { Key = "输出", Value = "4~20mA" }
        };

        var json = SpecContentCodec.Serialize(input);
        var parsed = SpecContentCodec.Parse(json, out var warning);

        Assert.Null(warning);
        Assert.Equal(input, parsed);
        Assert.Contains("量程", json);
    }
}
