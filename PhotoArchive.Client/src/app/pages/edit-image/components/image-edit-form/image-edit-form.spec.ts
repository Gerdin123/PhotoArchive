import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ImageEditForm } from './image-edit-form';

describe('ImageEditForm', () => {
  let component: ImageEditForm;
  let fixture: ComponentFixture<ImageEditForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ImageEditForm]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ImageEditForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
