# Why Apache 2.0

QuanTAlib grinds financial math at the instruction-cycle level. Circular buffers, incremental O(1) computation, hardware-aligned memory. Over 300 indicators, each designed to sprint through streaming data without allocating so much as a sneeze on the managed heap.

That kind of optimization is precisely what proprietary firms like to absorb, repackage, and patent.

License is not formality. License is structural defense.

## The Attack Vector

I once assumed open-source licensing was about generosity. Slap MIT on it, share with world, feel warm inside. Then I watched a hedge fund's legal team send a cease-and-desist to an open-source author for code that author wrote. The fund had patented a specific algorithmic optimization they found in the project, repackaged it, and then had the audacity to claim prior art.

This is not urban legend. This is Tuesday in financial software.

The attack works like this:

1. Proprietary firm downloads QuanTAlib
2. They notice a specific incremental computation pattern: say, the O(1) streaming Savitzky-Golay implementation using circular buffers
3. Legal department files a patent on that specific methodology
4. They send cease-and-desist to original author
5. Author discovers that "doing whatever you want" cuts both ways
6. Author's Friday evening is ruined. Possibly also Saturday

Permissive license without patent protection is an invitation printed on expensive cardstock.

## The MIT Temptation

MIT is 170 words. Beautiful in its brevity. Says users can do whatever they want, provided they keep copyright notice. I understand the appeal. I felt it myself. Two paragraphs, done, back to writing code.

The fatal flaw: no explicit patent grant. Zero protection against the scenario above. If someone patents a technique derived from your circular buffer implementation, MIT gives you the legal standing of a fortune cookie.

MIT works fine for frontend widgets and utility libraries where nobody is going to patent your string formatter. For a library that optimizes financial math with hardware-aware memory patterns and incremental algorithms? MIT is a t-shirt in a legal gunfight.

I have nothing against MIT. MIT did not hurt me. MIT simply does not solve this particular problem.

## The BSD Alternative

BSD 3-Clause shares MIT's structural weakness on patents. It adds a clause preventing users from plastering your name on their marketing materials. This prevents some shady exchange from stamping "Powered by QuanTAlib" on their homepage while their risk engine quietly produces incorrect signals because they modified a filter coefficient and told nobody.

That clause solves a marketing problem. It does not solve the intellectual property problem. Two different problems. Two different threat models.

## Why Apache 2.0 Specifically

Apache 2.0 provides structural protections that matter when your code will be consumed by entities with legal departments larger than your engineering team.

### Explicit Patent Grant (Section 3)

Every contributor explicitly grants a patent license to every user. This is not implied. Not assumed. Not "probably fine." Written in the actual text.

What this means in practice:

- Contributors cannot submit code and later claim patent rights over it
- Users receive a clear, irrevocable patent license for contributions they use
- Grant covers the specific contribution and its combination with existing work
- The word "irrevocable" is doing heavy lifting here, and it knows it

### Patent Retaliation (Section 3, Final Paragraph)

This is the nuclear deterrent. Worth quoting the operational logic:

If any entity uses QuanTAlib and then sues any contributor for patent infringement related to the Work, their license to use QuanTAlib **terminates immediately**. On the date litigation is filed. Not when judgment is rendered. Not after appeals. The moment the lawsuit hits the docket.

Mutually assured destruction. Bad actors can use library for commercial gain (which drives adoption, which is good), but they cannot weaponize legal system against creators. The moment they file patent suit, they lose right to use the software they built their system on.

I have watched a general counsel's face when this clause was explained to them. The expression was educational.

### Change Tracking (Section 4b)

Modified files must carry prominent notices stating they were changed. This creates audit trail. When derivative work surfaces in the wild producing subtly wrong RSI values because someone "optimized" the circular buffer logic, it is clear what was modified and by whom.

For a library where numerical correctness is the product: where a wrong coefficient in a filter produces trading signals that look plausible but bleed money slowly enough that you do not notice until Q3 reporting: traceability has value beyond legal compliance.

## The Honest Tradeoff

Apache 2.0 is roughly 4,300 words. MIT is 170. That is a 25x complexity increase. Apache requires attribution, license inclusion, change notices. This adds compliance friction for downstream users.

I will not pretend otherwise. The friction is real.

But consider the consumer. QuanTAlib will be consumed by hedge funds, proprietary trading desks, and platform vendors. These are entities that employ compliance officers whose entire job is reading license files. They have already read Apache 2.0. They have templates for it. The "friction" for these consumers is approximately zero.

The friction for a weekend hobbyist who just wants to calculate a moving average? Also approximately zero, because hobbyists do not file patents.

The friction exists in a narrow band of consumers who want to do something unusual with the code. For that narrow band, 4,300 words of clarity is better than 170 words of ambiguity.

## Side-by-Side

| Protection | MIT | BSD 3-Clause | Apache 2.0 |
|:---|:---:|:---:|:---:|
| Copyright protection | ✅ | ✅ | ✅ |
| Disclaimer of warranty | ✅ | ✅ | ✅ |
| Explicit patent grant | ❌ | ❌ | ✅ |
| Patent retaliation clause | ❌ | ❌ | ✅ |
| Name use restriction | ❌ | ✅ | ✅ |
| Change tracking requirement | ❌ | ❌ | ✅ |
| Contribution license terms | ❌ | ❌ | ✅ |

Six of seven protections versus two. The math is not subtle.

## What This Means for You

**Using QuanTAlib commercially?** Go ahead. Apache 2.0 explicitly permits commercial use, modification, distribution, and sublicensing. No phone call required. No royalties. No awkward conversations.

**Modifying QuanTAlib?** Note your changes in modified files. That is it. You are not required to open-source your modifications. You are not required to share your proprietary trading strategy that uses a custom indicator chain. Keep your secrets. Just mark what you changed.

**Building a product on QuanTAlib?** Include the license file and attribution. Your compliance officer already knows how to do this. If you do not have a compliance officer, the LICENSE file in repository root contains everything you need.

**Thinking about patenting something derived from QuanTAlib?** Read Section 3 carefully. Then read it again. Then perhaps reconsider.

## Bottom Line

Finance code is a target. Always has been. The firms that consume open-source math libraries are the same firms that maintain patent portfolios as competitive weapons.

Apache 2.0 does not restrict freedom. Anyone can use, modify, distribute, sell, and build empires on QuanTAlib. It restricts one specific behavior: using the legal system to attack the people who wrote the code you are profiting from.

That seems reasonable. Even to a curmudgeon.
