using System.Text;

namespace SimpleChat.Client;

public static class MessageSource
{
    private static string[] messageSource;
    private static Random random = new(int.Parse(string.Join(string.Empty, Guid.NewGuid().ToString().Select(char.IsNumber))));

    public static IEnumerable<string> GetMessages(int count = 10, int length = 5)
    {
        if (count < 0) throw new ArgumentException("Messages count can't be negative");
        if (length < 0) throw new ArgumentException("Messages length can't be negative");

        return MessagesGenerator(count, length);
    }

    private static IEnumerable<string> MessagesGenerator(int count, int length)
    {
        var builder = new StringBuilder(length);

        while(count-- > 0)
        {
            builder.Clear();

            for (int index = 0; index < length; index++)
            {
                builder.Append(messageSource[random.Next(0, messageSource.Length)] + " ");
            }

            yield return builder.ToString().Trim();
        }
    }

    static MessageSource()
    {
        messageSource = ("Lorem ipsum dolor sit amet, " +
            "consectetur adipiscing elit. Pellentesque bibendum" +
            " dui sed odio semper, id luctus metus facilisis. " +
            "Quisque vel ante sed diam varius ultrices in tristique nisl. " +
            "Proin egestas risus dapibus porttitor facilisis. " +
            "Ut tincidunt ultricies sapien, at dapibus lacus laoreet a. " +
            "Proin eget justo in ipsum sagittis vulputate non et justo. " +
            "Nulla pellentesque nulla sed ex vulputate interdum. " +
            "Fusce tempus fringilla nisl ac egestas. Sed vel bibendum nibh. " +
            "Duis auctor augue augue, ac commodo arcu luctus quis. " +
            "Curabitur at semper erat, malesuada tincidunt nibh. " +
            "Phasellus lacus odio, vestibulum id massa vitae, " +
            "venenatis suscipit eros. Nullam imperdiet tempus est.").
            ToLowerInvariant()
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
