import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PeopleManager } from './people-manager';

describe('PeopleManager', () => {
  let component: PeopleManager;
  let fixture: ComponentFixture<PeopleManager>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PeopleManager]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PeopleManager);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
