import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReportDateRangeFilter } from './report-date-range-filter.component';

describe('ReportDateRangeFilter', () => {
  function render(from: string, to: string): ComponentFixture<ReportDateRangeFilter> {
    const fixture = TestBed.createComponent(ReportDateRangeFilter);
    fixture.componentRef.setInput('from', from);
    fixture.componentRef.setInput('to', to);
    fixture.detectChanges();
    return fixture;
  }

  it('AC163: emits the form values when applied', () => {
    const fixture = render('2026-08-01', '2026-08-27');
    const emitted: { from: string; to: string }[] = [];
    fixture.componentInstance.apply.subscribe((value) => emitted.push(value));

    fixture.componentInstance.form.controls.from.setValue('2026-07-01');
    (fixture.nativeElement as HTMLElement).querySelector('form')!.dispatchEvent(new Event('submit'));

    expect(emitted).toEqual([{ from: '2026-07-01', to: '2026-08-27' }]);
  });
});
