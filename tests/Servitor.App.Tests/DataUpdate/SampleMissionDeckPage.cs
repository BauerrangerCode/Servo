using System.Text;

namespace Servitor.App.Tests.DataUpdate;

/// <summary>
/// A small invented page in the structure of the real Mission Deck page (same CSS classes, made-up names and
/// rules), so the scraper is tested without storing the real page.
/// </summary>
public static class SampleMissionDeckPage
{
    public static string Page(int primaryCount = 15, int secondaryCount = 10)
    {
        var page = new StringBuilder("<html><h2 id=\"Primary-Mission-deck\">Primary Mission deck</h2><div class=\"Columns2\">");
        for (var i = 0; i < primaryCount; i++)
        {
            page.Append(PrimaryCard($"MISSION {i}", "TakeAndHold", "Disruption", "<div class=\"caPmHdr\">ANY BATTLE ROUND</div><div class=\"caPmWhen\"><b>WHEN:</b> End of your turn.</div><div class=\"caPmScore\"><span>You hold <b>things</b>.</span><span class=\"caPmVP\">2VP</span></div>"));
        }

        page.Append("</div><h2 id=\"Deployment-deck\">Deployment deck</h2><div class=\"Columns2\">");
        foreach (var name in new[] { "HAMMER AND ANVIL", "DAWN OF WAR", "SEARCH AND DESTROY", "CRUCIBLE OF BATTLE", "UNKNOWN LAYOUT" })
        {
            page.Append($"<div class=\"cgCard_pad\"><div class=\"cgCardCA7\"><div class=\"ca7Head ca7HeadDP\"><div class=\"ca7HeadText\"><div class=\"ca7Name\">{name}</div></div><div class=\"ca7Type\">DEPLOYMENT CARD</div></div><div class=\"ca7Pic\"><img src=\"x.png\"></div></div></div>");
        }

        page.Append("</div><h2 id=\"Secondary-Mission-deck\">Secondary Mission deck</h2><div class=\"Columns2\">");
        for (var i = 0; i < secondaryCount; i++)
        {
            page.Append(SecondaryCard($"CARD {i}", false, "<div class=\"caPmHdr\">ANY BATTLE ROUND</div><div class=\"caPmWhen\"><b>WHEN:</b> End of a turn.</div><div class=\"caPmScore\"><span>You did a thing.</span><span class=\"caPmVP\">3VP</span></div>"));
        }

        page.Append("</div><h2 id=\"Twist-deck\">Twist deck</h2><div class=\"cgCard_pad\">ignored</div></html>");
        return page.ToString();
    }

    public static string PrimaryCard(string name, string yours, string opponent, string body, string intro = "") =>
        $"<div class=\"cgCard_pad\"><div class=\"cgCardCA7\"><div class=\"ca7Head ca7HeadPM ca7Head{yours}\"><div class=\"ca7HeadText\"><div class=\"ca7Name\">{name}</div><div class=\"ca7Type\">Primary Mission</div></div></div>"
        + "<p class=\"ShowFluff ca7Legend\">Some <i>legend</i> text.</p>"
        + $"<div class=\"ca7Body\">{intro}<div class=\"caPmBlock\">{body}</div></div>"
        + $"<div class=\"ca7Fd\"><div class=\"ca7FdPlayer\"><div class=\"{yours}2 ca7FdTip\" data-fd=\"{yours}\"></div></div><div class=\"ca7FdOpp\"><div class=\"ca7FdLbl\">Opponent</div><div class=\"{opponent}2 ca7FdTip\" data-fd=\"{opponent}\"></div></div></div></div></div>";

    public static string SecondaryCard(string name, bool fixedEligible, string body, string intro = "", string tail = "") =>
        $"<div class=\"cgCard_pad\"><div class=\"cgCardCA7\"><div class=\"ca7Head ca7HeadSM\"><div class=\"ca7HeadText\"><div class=\"ca7Name\">{name}</div></div>"
        + (fixedEligible ? "<div class=\"ca7Fixed tooltip\" title=\"Fixed Mission\"></div>" : "")
        + "<div class=\"ca7Type\"><span class=\"ca7TypeSolo\">Secondary Mission</span></div></div>"
        + $"<p class=\"ShowFluff ca7Legend\">A legend.</p><div class=\"ca7Body\">{intro}<div class=\"caPmBlock\">{body}</div>{tail}</div></div></div>";
}
