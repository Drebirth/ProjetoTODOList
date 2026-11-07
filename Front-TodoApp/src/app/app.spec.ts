import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App root', () => {
  it('contains only the router outlet and no shared authentication or task-editor state', async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('router-outlet')).not.toBeNull();
    expect(root.querySelector('input')).toBeNull();
    expect(root.querySelector('[role="dialog"]')).toBeNull();
    expect(Object.keys(fixture.componentInstance)).toEqual([]);
  });
});
