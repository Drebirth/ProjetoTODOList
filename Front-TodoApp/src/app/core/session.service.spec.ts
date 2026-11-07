import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SessionService } from './session.service';

describe('SessionService during server rendering', () => {
  it('does not read or write sessionStorage when PLATFORM_ID is server', () => {
    const getItem = vi.spyOn(sessionStorage, 'getItem');
    const setItem = vi.spyOn(sessionStorage, 'setItem');
    const removeItem = vi.spyOn(sessionStorage, 'removeItem');
    TestBed.configureTestingModule({
      providers: [SessionService, { provide: PLATFORM_ID, useValue: 'server' }],
    });

    const session = TestBed.inject(SessionService);
    session.restore();
    expect(session.token()).toBeNull();
    session.setToken('server-token');
    expect(session.token()).toBe('server-token');
    session.clear();
    expect(session.token()).toBeNull();

    expect(getItem).not.toHaveBeenCalled();
    expect(setItem).not.toHaveBeenCalled();
    expect(removeItem).not.toHaveBeenCalled();
  });
});
