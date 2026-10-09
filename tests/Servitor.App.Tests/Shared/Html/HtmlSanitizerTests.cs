using Servitor.RulesData.Html;

namespace Servitor.App.Tests.Shared.Html;

public class HtmlSanitizerTests
{
    [Fact]
    public void Keeps_allow_listed_tags()
    {
        var result = HtmlSanitizer.Sanitize("<b>WHEN:</b> Your Shooting phase.<br><ul><li>One effect.</li></ul>");

        Assert.Equal("<b>WHEN:</b> Your Shooting phase.<br /><ul><li>One effect.</li></ul>", result);
    }

    [Fact]
    public void Preserves_a_safe_span_class()
    {
        var result = HtmlSanitizer.Sanitize("Target the <span class=\"kwb\">INFANTRY</span> unit.");

        Assert.Equal("Target the <span class=\"kwb\">INFANTRY</span> unit.", result);
    }

    [Fact]
    public void Strips_script_tags_and_their_content_entirely()
    {
        var result = HtmlSanitizer.Sanitize("Safe text<script>alert('xss')</script> more text");

        Assert.Equal("Safe text more text", result);
        Assert.DoesNotContain("alert", result);
    }

    [Fact]
    public void Unwraps_disallowed_tags_but_keeps_their_text()
    {
        var result = HtmlSanitizer.Sanitize("<a href=\"javascript:alert(1)\">Click here</a>");

        Assert.Equal("Click here", result);
        Assert.DoesNotContain("javascript", result);
    }

    [Fact]
    public void Strips_unsafe_characters_from_a_class_attribute()
    {
        var result = HtmlSanitizer.Sanitize("<span class=\"kwb\" onclick=\"evil()\">x</span>");

        Assert.Equal("<span class=\"kwb\">x</span>", result);
        Assert.DoesNotContain("onclick", result);
    }

    [Fact]
    public void Empty_input_returns_empty_string()
    {
        Assert.Equal(string.Empty, HtmlSanitizer.Sanitize(""));
    }
}
