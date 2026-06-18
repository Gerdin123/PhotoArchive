import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThumbnailCard } from './thumbnail-card';

describe('ThumbnailCard', () => {
  let component: ThumbnailCard;
  let fixture: ComponentFixture<ThumbnailCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ThumbnailCard]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ThumbnailCard);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
