using UnityEngine;

namespace CarMaxSpecialist
{
    /// <summary>Everything he (and everyone around him) says.</summary>
    public static class Lines
    {
        public static string Pick(string[] list) => list[Random.Range(0, list.Length)];

        public static readonly string[] ClockIn =
        {
            "Clocked in. Let's make it a great CarMax day!",
            "Clocked in. 47 unread emails.",
            "Clocked in. Who parked in the service lane?",
            "Clocked in. Coffee machine's broken again."
        };

        public static readonly string[] Corporate =
        {
            "Let's circle back on that.",
            "Do the right thing. Every time.",
            "Respect for every associate!",
            "Let's take this offline.",
            "We're a family here.",
            "This could've been an email.",
            "Per the new SOP...",
            "Quick sync? It'll take 5 minutes.",
            "Let's put a pin in that.",
            "Remember to complete your Associate Survey!",
            "Love Your Car. Love Your Job.",
            "Moving the needle!",
            "Low-hanging fruit, people."
        };

        public static readonly string[] OvertimeStart =
        {
            "<color=#FFD040>OVERTIME</color>\n<size=70%>Time and a half. Your family will understand.</size>",
            "<color=#FFD040>OVERTIME</color>\n<size=70%>Manager: \"We really need you to stay late tonight.\"</size>",
            "<color=#FFD040>OVERTIME</color>\n<size=70%>Mandatory. Not optional. We're a team.</size>"
        };

        public static readonly string[] OvertimeGrind =
        {
            "Hour ten. I can see sounds.",
            "My kids think I'm a rumor.",
            "Is it Tuesday or Saturday?",
            "Just one more report...",
            "I'll sleep at the dealership."
        };

        public static readonly string[] Customers =
        {
            "I WANT TO SPEAK TO YOUR MANAGER!",
            "You said it'd take an HOUR!",
            "Why is my car on a lift?!",
            "The website said a different price!",
            "I've been waiting since 8 AM!",
            "Is it done yet? How about now?",
            "My cousin's a mechanic, he says you're wrong.",
            "I'm leaving a one-star review!",
            "Why does an oil change cost THAT?",
            "I've been a loyal customer for 20 years!",
            "That noise was NOT there before!",
            "Can I get a loaner? A NICE one?"
        };

        public static readonly string[] Praise =
        {
            "Great job, team! Pizza party!",
            "You're a rockstar! (no raise)",
            "Kudos! Shared in the team channel.",
            "Exceeds expectations! Pizza's in the break room.",
            "Associate of the Month! (certificate only)"
        };

        public static readonly string[] Scold =
        {
            "Per my last email, this was due today.",
            "Let's set up a 1:1 to discuss.",
            "I'm not mad, just disappointed.",
            "This will be noted in your review.",
            "Going forward, please prioritize."
        };

        public static readonly string[] Burnout =
        {
            "I need to talk to HR.",
            "I didn't go to school for this.",
            "Two weeks notice. In my head.",
            "*slams desk*",
            "I'm putting in for PTO. Denied? Of course."
        };

        public static readonly string[] Names =
        {
            "Brad", "Tyler", "Jessica from Sales", "Kyle (new hire)", "Mike", "Dustin", "Brittany", "Chad", "Gary"
        };

        public static readonly string[] Lazy =
        {
            "brb, smoke break",
            "that's not my department",
            "I'm on my lunch",
            "can you show me how to do this again?",
            "I'm gonna head out early",
            "*scrolling phone*",
            "did you see the game last night?",
            "my tool's in my truck",
            "I'll get to it after break"
        };

        public static readonly string[] Helpful =
        {
            "On it.",
            "Fine, I'll help. Once.",
            "You owe me lunch.",
            "Is this billable?"
        };

        public static readonly string[] CallIn =
        {
            "Can I get some help out here?!",
            "All hands on deck!",
            "Anybody? Hello?",
            "Paging the service team..."
        };

        public static readonly string[] ClockOut =
        {
            "it's 4:59, I'm out",
            "clocking out, peace",
            "shift's over, not my problem"
        };

        public static readonly string[] Diy =
        {
            "Fine. I'll do it myself.",
            "If you want something done right...",
            "Go home. I've got it.",
            "Nobody else is going to do it."
        };

        public static readonly string[] Backordered =
        {
            "BACKORDERED",
            "BACKORDERED - ETA: ???",
            "BACKORDERED - wrong part shipped"
        };
    }
}
