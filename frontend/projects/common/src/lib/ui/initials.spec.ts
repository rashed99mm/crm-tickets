import { initialsOf } from './initials';

describe('initialsOf', () => {
  it('takes the first and last word, not every word', () => {
    expect(initialsOf('Layla Haddad')).toBe('LH');
    // Three letters in a 32px circle is a smudge, so a middle name is dropped rather than included.
    expect(initialsOf('Mohamed Ahmed Rashed')).toBe('MR');
  });

  it('gives one letter for one word', () => {
    expect(initialsOf('Layla')).toBe('L');
  });

  it('tolerates the whitespace a form actually sends', () => {
    expect(initialsOf('  Layla   Haddad  ')).toBe('LH');
  });

  /**
   * The case that matters: an absent name must render as an empty circle, not as a `?`. A question
   * mark reads as a value that failed to load, which is a different and more alarming statement
   * than "this record has no name".
   */
  it('returns nothing for a name that is not there', () => {
    expect(initialsOf('')).toBe('');
    expect(initialsOf('   ')).toBe('');
    expect(initialsOf(null)).toBe('');
    expect(initialsOf(undefined)).toBe('');
  });

  it('upper-cases what it finds', () => {
    expect(initialsOf('layla haddad')).toBe('LH');
  });

  /** Arabic has no case distinction, so the same call must leave the letters untouched. */
  it('leaves a script with no case alone', () => {
    expect(initialsOf('ليلى حداد')).toBe('لح');
  });
});
