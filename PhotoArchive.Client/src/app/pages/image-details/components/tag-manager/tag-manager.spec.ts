import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TagManager } from './tag-manager';

describe('TagManager', () => {
  let component: TagManager;
  let fixture: ComponentFixture<TagManager>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [TagManager]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TagManager);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
