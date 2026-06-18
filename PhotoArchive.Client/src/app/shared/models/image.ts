import { Tag } from "./tag";
import { Person } from "./person";

export interface Image{
    id: number;
    title: string;
    url: string;
    date: Date;
    tags: Tag[];
    people: Person[];
}