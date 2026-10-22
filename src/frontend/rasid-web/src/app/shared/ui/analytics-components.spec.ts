import { TestBed } from '@angular/core/testing';
import { AnimatedNumber } from './animated-number';
import { Sparkline } from './sparkline';
import { TrendChip } from './trend-chip';

describe('analytics components', () => {
  it('trend chip shows direction, sign and unit', async () => {
    const fixture = TestBed.createComponent(TrendChip);
    fixture.componentRef.setInput('value', 12.34);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('+12.3%');
    expect(fixture.nativeElement.querySelector('.trend--up')).not.toBeNull();

    fixture.componentRef.setInput('value', -0.4);
    fixture.componentRef.setInput('unit', 'pts');
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('−0.4 pts');
    expect(fixture.nativeElement.querySelector('.trend--down')).not.toBeNull();
  });

  it('trend chip says "new" when there is nothing to compare with', async () => {
    const fixture = TestBed.createComponent(TrendChip);
    fixture.componentRef.setInput('value', null);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent.trim()).toBe('new');
  });

  it('animated number renders N/A for a missing rate and the final value under reduced motion', async () => {
    const fixture = TestBed.createComponent(AnimatedNumber);
    fixture.componentRef.setInput('format', 'rate');
    fixture.componentRef.setInput('value', null);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toBe('N/A');
    expect(fixture.nativeElement.getAttribute('aria-label')).toBe('N/A');

    fixture.componentRef.setInput('value', 2.5);
    await fixture.whenStable();
    expect(fixture.nativeElement.getAttribute('aria-label')).toBe('2.50%');
  });

  it('sparkline draws a line for two or more points only', async () => {
    const fixture = TestBed.createComponent(Sparkline);
    fixture.componentRef.setInput('values', [1, 4, 2, 8]);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('path.line')?.getAttribute('d')).toMatch(/^M0\.0,/);

    fixture.componentRef.setInput('values', [5]);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('path.line')).toBeNull();
  });
});
