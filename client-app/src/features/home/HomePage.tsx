import { Container } from "semantic-ui-react";
import { Link } from "react-router-dom";
export default function HomePage(){
    return(
        <Container style={{marginTop:'7em'}}>
            <h1>home page</h1>
            <h3>go to<Link to='/activities'>
                Activities
                </Link></h3>
        </Container>

    )
}