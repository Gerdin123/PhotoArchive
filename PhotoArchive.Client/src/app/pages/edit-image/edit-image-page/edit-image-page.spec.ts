import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditImagePage } from './edit-image-page';

describe('EditImagePage', () => {
  let component: EditImagePage;
  let fixture: ComponentFixture<EditImagePage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [EditImagePage]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EditImagePage);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
