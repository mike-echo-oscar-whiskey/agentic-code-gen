import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
  });

  it('shows the goal composer before any run has started', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('app-goal-composer')).toBeTruthy();
    expect(element.querySelector('app-agent-timeline')).toBeFalsy();
  });

  it('keeps the run button disabled until a goal is typed', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const button = element.querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(button?.disabled).toBe(true);
  });

  it('enables the run button once a goal is present', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const textarea = element.querySelector<HTMLTextAreaElement>('textarea');
    textarea!.value = 'summarise a Met artwork';
    textarea!.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    fixture.detectChanges();

    const button = element.querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(button?.disabled).toBe(false);
  });
});
