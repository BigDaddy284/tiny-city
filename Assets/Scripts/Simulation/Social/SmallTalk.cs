using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Social
{
    /// <summary>
    /// Built-in conversation lines for when the AI is unavailable or slow. Chosen deterministically from the
    /// conversation and line number, so a town without AI behaves the same every time.
    /// </summary>
    public static class SmallTalk
    {
        private static readonly string[] Greetings =
        {
            "Hello, {other}! Lovely to see you.",
            "Oh, {other}! Fancy bumping into you here.",
            "Good to see you, {other}. How are you keeping?",
        };

        private static readonly string[] FirstGreetings =
        {
            "Hello there, I'm {me}. I don't think we've properly met.",
            "Hi! I'm {me}. You must be {other}?",
        };

        private static readonly string[] Replies =
        {
            "Hello, {other}! Not bad at all, thanks for asking.",
            "Oh, hello {other}. Can't complain, can't complain.",
            "All the better for seeing you, {other}.",
        };

        private static readonly string[] FirstReplies =
        {
            "Nice to meet you, {other}. I'm {me}.",
            "That's me! Lovely to meet you properly, {other}.",
        };

        private static readonly string[] Chatter =
        {
            "Isn't the village looking lovely {part}?",
            "I've been meaning to sit by the pond more often.",
            "Have you seen how the community garden is coming along?",
            "I could murder a cup of tea, honestly.",
            "It's been a busy old week, hasn't it?",
            "Did you hear the birds this morning? Such a racket!",
            "I keep meaning to tidy up, but there's always something.",
            "You always seem to know what's going on round here.",
        };

        private static readonly string[] Farewells =
        {
            "Well, I'd better get on. See you soon, {other}!",
            "Lovely chatting, {other}. Take care.",
            "Right, I'll let you get on. Bye for now!",
        };

        /// <summary>The line for <paramref name="lineIndex"/> of a conversation with <paramref name="maxLines"/> lines.</summary>
        public static (string text, ConversationTone tone) Line(Resident speaker, Resident listener, bool firstMeeting, int conversationNumber, int lineIndex, int maxLines, GameTime now)
        {
            string template;
            if (lineIndex == 0)
            {
                template = firstMeeting ? Pick(FirstGreetings, conversationNumber) : Pick(Greetings, conversationNumber);
            }
            else if (lineIndex == 1)
            {
                template = firstMeeting ? Pick(FirstReplies, conversationNumber) : Pick(Replies, conversationNumber);
            }
            else if (lineIndex >= maxLines - 1)
            {
                template = Pick(Farewells, conversationNumber + lineIndex);
            }
            else
            {
                template = Pick(Chatter, conversationNumber + lineIndex);
            }

            var text = template
                .Replace("{me}", speaker.FirstName)
                .Replace("{other}", listener.FirstName)
                .Replace("{part}", now.Hour < 12 ? "this morning" : now.Hour < 18 ? "this afternoon" : "this evening");
            return (text, lineIndex % 3 == 0 ? ConversationTone.Warm : ConversationTone.Neutral);
        }

        private static string Pick(string[] options, int n) => options[((n % options.Length) + options.Length) % options.Length];
    }
}
