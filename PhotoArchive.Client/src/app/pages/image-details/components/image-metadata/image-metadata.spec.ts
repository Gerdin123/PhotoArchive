import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ImageMetadata } from './image-metadata';

describe('ImageMetadata', () => {
  let component: ImageMetadata;
  let fixture: ComponentFixture<ImageMetadata>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ImageMetadata]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ImageMetadata);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
