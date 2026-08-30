/**
 * A person's initials, for the avatar marks the mockups draw as photographs.
 *
 * Nothing in this product stores a photograph. The two honest substitutes are initials or one
 * generic glyph repeated on every row; initials win because they are derived from a real field, so
 * two adjacent rows look different and an agent switching between two profiles can see that they
 * did.
 *
 * First and last word, so "Mohamed Ahmed Rashed" reads `MR` rather than `MAR` — three letters in a
 * 32px circle is a smudge. A single word gives one letter, and an empty or whitespace-only name
 * gives an empty string rather than a placeholder character: the caller's circle then renders empty,
 * which is the correct rendering of "there is no name", where a `?` would read as a fetch that went
 * wrong.
 *
 * Lives in `common` rather than in a component because three screens need it — the customer list's
 * rows, the customer profile's band and anything that follows — and three copies of a string
 * derivation is how the three quietly stop agreeing.
 */
export function initialsOf(name: string | null | undefined): string {
  const words = (name ?? '').trim().split(/\s+/).filter(Boolean);

  if (words.length === 0) {
    return '';
  }

  const first = words[0][0];
  const last = words.length > 1 ? words[words.length - 1][0] : '';

  // `toLocaleUpperCase` rather than `toUpperCase`: Turkish dotted/dotless i is the classic case
  // where the two disagree, and a name is exactly the kind of string that hits it.
  return (first + last).toLocaleUpperCase();
}
