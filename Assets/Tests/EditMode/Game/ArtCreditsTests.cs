using NUnit.Framework;
using Wildgrove.Game;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// The CC BY plates oblige the shipped build to name the work, its author,
    /// the licence and a link to it, and to say the work was changed. A credit
    /// that loses a field is a licence breach that nothing else would catch —
    /// the colophon would simply render a shorter line.
    /// </summary>
    public class ArtCreditsTests
    {
        [Test]
        public void Licensed_NamesEveryWorkInFull()
        {
            Assert.That(ArtCredits.Licensed, Is.Not.Empty);

            foreach (var work in ArtCredits.Licensed)
            {
                Assert.That(work.title, Is.Not.Null.And.Not.Empty, "a work must be named");
                Assert.That(work.author, Is.Not.Null.And.Not.Empty, work.title + " needs its author");
                Assert.That(work.licence, Does.StartWith("CC BY"), work.title + " needs its licence");
                Assert.That(work.licenceUrl, Does.Contain("creativecommons.org"),
                    work.title + " needs a link to the licence");
                Assert.That(work.change, Is.Not.Null.And.Not.Empty,
                    work.title + " must say how it was changed");
            }
        }

        [Test]
        public void Line_ReadsAsACompleteCredit()
        {
            foreach (var work in ArtCredits.Licensed)
            {
                var line = ArtCredits.Line(work);
                Assert.That(line, Does.Contain(work.title));
                Assert.That(line, Does.Contain(work.author));
                Assert.That(line, Does.Contain(work.licence));
                Assert.That(line, Does.Contain(work.licenceUrl));
                Assert.That(line, Does.Contain(work.change));
            }
        }

        [Test]
        public void Line_OnNothing_IsEmptyRatherThanThrowing()
        {
            // The colophon renders whatever the list holds; a null entry must
            // not take the sheet down mid-draw.
            Assert.That(ArtCredits.Line(null), Is.Empty);
        }

        [Test]
        public void Preamble_AndPublicDomainNote_AreSaid()
        {
            Assert.That(ArtCredits.Preamble, Is.Not.Null.And.Not.Empty);
            Assert.That(ArtCredits.PublicDomainNote, Is.Not.Null.And.Not.Empty);
        }
    }
}
