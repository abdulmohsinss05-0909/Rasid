import { displayRule, normalizeRule } from './rule-rules';

describe('normalizeRule', () => {
  it('normalises hashtags and accounts', () => {
    expect(normalizeRule('Hashtag', '#UAEAid')).toEqual({ value: 'UAEAid' });
    expect(normalizeRule('Account', '@some_account')).toEqual({ value: 'some_account' });
    expect(normalizeRule('Keyword', '  humanitarian    aid ')).toEqual({ value: 'humanitarian aid' });
  });

  it('accepts Arabic keywords and hashtags', () => {
    expect(normalizeRule('Keyword', 'مساعدات إنسانية')).toEqual({ value: 'مساعدات إنسانية' });
    expect(normalizeRule('Hashtag', '#الإمارات')).toEqual({ value: 'الإمارات' });
  });

  it('rejects values that could inject X query operators', () => {
    expect('error' in normalizeRule('Keyword', 'aid" OR from:x')).toBe(true);
    expect('error' in normalizeRule('Keyword', '-aid')).toBe(true);
    expect('error' in normalizeRule('Account', 'bad-name')).toBe(true);
    expect('error' in normalizeRule('Hashtag', 'two words')).toBe(true);
  });

  it('displays rules with their prefix', () => {
    expect(displayRule('Hashtag', 'UAEAid')).toBe('#UAEAid');
    expect(displayRule('Account', 'x')).toBe('@x');
    expect(displayRule('Keyword', 'aid')).toBe('aid');
  });
});
